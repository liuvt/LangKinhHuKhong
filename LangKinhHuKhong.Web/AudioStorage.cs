using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace LangKinhHuKhong;

/// <summary>
/// R2 is PRIVATE: never enable public bucket access. File references are stored
/// as r2:stories/{slug}--{story-id}/Episodes/Tap-000001-{object-id}.mp3.
/// Old r2:audio/{id}/full.mp3 references and local files remain readable.
/// Authorization is ALWAYS enforced by Program.cs before calling StreamAsync.
/// </summary>
public sealed class AudioStorage : IDisposable
{
    private readonly Database database;
    private readonly IAmazonS3? s3;
    private readonly string bucket;
    private readonly ILogger<AudioStorage> logger;

    public bool IsR2 => s3 is not null;
    public string Mode => IsR2 ? "R2" : "Local";
    public string Bucket => IsR2 ? bucket : "";

    public AudioStorage(IConfiguration configuration, Database database, ILogger<AudioStorage> logger)
    {
        this.database = database;
        this.logger = logger;
        var mode = (configuration["LKH_AUDIO_STORAGE"] ?? "Local").Trim();
        if (mode.Equals("Local", StringComparison.OrdinalIgnoreCase)) { bucket = ""; return; }
        if (!mode.Equals("R2", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LKH_AUDIO_STORAGE chỉ hỗ trợ Local hoặc R2.");

        var account = configuration["R2_ACCOUNT_ID"]?.Trim();
        var accessKey = configuration["R2_ACCESS_KEY_ID"]?.Trim();
        var secretKey = configuration["R2_SECRET_ACCESS_KEY"];
        bucket = configuration["R2_BUCKET_NAME"]?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(accessKey) ||
            string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(bucket))
            throw new InvalidOperationException("R2 thiếu R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY hoặc R2_BUCKET_NAME. Kiểm tra biến môi trường.");
        if (!Regex.IsMatch(account, "^[a-fA-F0-9]{32}$"))
            throw new InvalidOperationException("R2_ACCOUNT_ID cần là Account ID Cloudflare gồm 32 ký tự hex.");
        if (!Regex.IsMatch(bucket, "^[a-z0-9][a-z0-9.-]{1,62}[a-z0-9]$"))
            throw new InvalidOperationException("Tên R2 bucket không hợp lệ.");

        // For special jurisdictions (EU/US), provide R2_ENDPOINT explicitly.
        var endpoint = configuration["R2_ENDPOINT"]?.Trim();
        if (string.IsNullOrEmpty(endpoint)) endpoint = $"https://{account}.r2.cloudflarestorage.com";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
            url.UserInfo.Length > 0 || !(url.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("R2_ENDPOINT cần là endpoint HTTPS S3 do Cloudflare cung cấp.");
        s3 = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
            Timeout = TimeSpan.FromMinutes(15)
        });
    }

    private const string R2AudioPattern = @"^(audio/[a-f0-9]{32}/(full|preview)\.mp3|stories/[a-z0-9]+(?:-[a-z0-9]+)*--[a-f0-9]{32}/(Episodes|Previews)/Tap-[0-9]{6}-[a-f0-9]{32}\.mp3)$";
    private const string R2CoverPattern = @"^stories/[a-z0-9]+(?:-[a-z0-9]+)*--[a-f0-9]{32}/Image/cover-[a-f0-9]{32}\.(png|jpg|webp)$";
    private const string HlsPrefixPattern = @"^stories/[a-z0-9]+(?:-[a-z0-9]+)*--[a-f0-9]{32}/HLS/Tap-[0-9]{6}-[a-f0-9]{32}$";
    private static bool IsHlsRef(string reference) =>
        (reference.StartsWith("r2:", StringComparison.Ordinal) && Regex.IsMatch(reference[3..], HlsPrefixPattern)) ||
        Regex.IsMatch(reference, @"^hls-local:[a-f0-9]{32}$");
    private static bool IsHlsFile(string name) => name is "index.m3u8" or "key.bin" ||
        Regex.IsMatch(name, @"^segment-[0-9]{6}\.ts$");
    private const string PrefixPattern = @"^stories/[a-z0-9]+(?:-[a-z0-9]+)*--[a-f0-9]{32}$";

    public static string StoryPrefix(string slug, string id) => $"stories/{slug}--{id}";
    private static void CheckPrefix(string prefix)
    {
        if (!Regex.IsMatch(prefix, PrefixPattern)) throw new InvalidOperationException("Thư mục bộ truyện không hợp lệ.");
    }
    private static (string full, string preview) EpisodeKeys(string prefix, string episodeId, int episodeNumber)
    {
        CheckPrefix(prefix);
        if (!Regex.IsMatch(episodeId, "^[a-f0-9]{32}$") || episodeNumber is < 1 or > 100000)
            throw new InvalidOperationException("Mã tập hoặc số tập không hợp lệ.");
        string file = $"Tap-{episodeNumber:D6}-{episodeId}.mp3";
        return ($"{prefix}/Episodes/{file}", $"{prefix}/Previews/{file}");
    }

    public async Task<(string full, string preview)> SaveEpisodeAsync(string fullPath, string previewPath,
        string storyPrefix, string episodeId, int episodeNumber, CancellationToken ct)
    {
        var fileNameFull = episodeId + "-full.mp3";
        var fileNamePreview = episodeId + "-preview.mp3";
        if (!IsR2)
        {
            File.Copy(fullPath, Path.Combine(database.AudioRoot, fileNameFull), overwrite: false);
            try { File.Copy(previewPath, Path.Combine(database.AudioRoot, fileNamePreview), overwrite: false); }
            catch { File.Delete(Path.Combine(database.AudioRoot, fileNameFull)); throw; }
            return (fileNameFull, fileNamePreview);
        }
        // Unique object IDs keep simultaneous retries isolated, protecting the active DB reference.
        var (fullKey, previewKey) = EpisodeKeys(storyPrefix, Guid.NewGuid().ToString("N"), episodeNumber);
        try
        {
            await PutAsync(fullKey, fullPath, "audio/mpeg", ct);
            await PutAsync(previewKey, previewPath, "audio/mpeg", ct);
            return ("r2:" + fullKey, "r2:" + previewKey);
        }
        catch
        {
            await DeleteBestEffortAsync("r2:" + fullKey, "r2:" + previewKey);
            throw;
        }
    }

    // Copy legacy R2 objects into the per-story prefix without removing originals.
    // Called only after Admin authorization and a DB lookup in Program.cs.
    public async Task<(string full, string preview)> OrganizeEpisodeAsync(
        string currentFull, string currentPreview, string storyPrefix, string episodeId,
        int episodeNumber, CancellationToken ct)
    {
        if (!IsR2 || s3 is null) throw new InvalidOperationException("Chưa bật Cloudflare R2.");
        if (!currentFull.StartsWith("r2:", StringComparison.Ordinal) &&
            !currentPreview.StartsWith("r2:", StringComparison.Ordinal))
        {
            if (!IsSafeLocalName(currentFull) || !IsSafeLocalName(currentPreview))
                throw new InvalidOperationException("File local không hợp lệ.");
            var sourceFull = Path.Combine(database.AudioRoot, currentFull);
            var sourcePreview = Path.Combine(database.AudioRoot, currentPreview);
            if (!File.Exists(sourceFull) || !File.Exists(sourcePreview))
                throw new FileNotFoundException("Thiếu file local gốc hoặc preview.");
            return await SaveEpisodeAsync(sourceFull, sourcePreview, storyPrefix, episodeId, episodeNumber, ct);
        }
        if (!currentFull.StartsWith("r2:", StringComparison.Ordinal) ||
            !currentPreview.StartsWith("r2:", StringComparison.Ordinal) ||
            !Regex.IsMatch(currentFull[3..], R2AudioPattern) ||
            !Regex.IsMatch(currentPreview[3..], R2AudioPattern))
            throw new InvalidOperationException("File R2 cũ không hợp lệ.");
        // Use a fresh key on every migration attempt so a failed concurrent retry
        // cannot delete an object that another successful operation committed.
        var (newFull, newPreview) = EpisodeKeys(storyPrefix, Guid.NewGuid().ToString("N"), episodeNumber);
        if (currentFull[3..] == newFull && currentPreview[3..] == newPreview)
            throw new InvalidOperationException("Tập đã nằm đúng thư mục truyện.");
        try
        {
            await s3.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = bucket, SourceKey = currentFull[3..],
                DestinationBucket = bucket, DestinationKey = newFull
            }, ct);
            await s3.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = bucket, SourceKey = currentPreview[3..],
                DestinationBucket = bucket, DestinationKey = newPreview
            }, ct);
            return ("r2:" + newFull, "r2:" + newPreview);
        }
        catch
        {
            await DeleteBestEffortAsync("r2:" + newFull, "r2:" + newPreview);
            throw;
        }
    }

    public async Task<string> SaveCoverAsync(string storyPrefix, string filePath, string extension, CancellationToken ct)
    {
        CheckPrefix(storyPrefix);
        if (extension is not (".png" or ".jpg" or ".webp"))
            throw new InvalidDataException("Định dạng ảnh không được hỗ trợ.");
        var fileName = $"cover-{Guid.NewGuid():N}{extension}";
        if (!IsR2)
        {
            var root = Path.Combine(database.DataRoot, "covers-private");
            Directory.CreateDirectory(root);
            File.Copy(filePath, Path.Combine(root, fileName));
            return "cover-local:" + fileName;
        }
        var key = $"{storyPrefix}/Image/{fileName}";
        await PutAsync(key, filePath, extension switch
        {
            ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg"
        }, ct);
        return "r2:" + key;
    }

    public async Task StreamCoverAsync(HttpContext context, string reference)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        var ext = Path.GetExtension(reference).ToLowerInvariant();
        string contentType = ext switch
        {
            ".png" => "image/png", ".jpg" => "image/jpeg", ".webp" => "image/webp", _ => ""
        };
        if (contentType == "") { context.Response.StatusCode = 404; return; }
        if (reference.StartsWith("cover-local:", StringComparison.Ordinal))
        {
            var name = reference["cover-local:".Length..];
            if (!Regex.IsMatch(name, @"^cover-[a-f0-9]{32}\.(png|jpg|webp)$")) { context.Response.StatusCode = 404; return; }
            var path = Path.Combine(database.DataRoot, "covers-private", name);
            if (!File.Exists(path)) { context.Response.StatusCode = 404; return; }
            await Results.File(path: path, contentType: contentType).ExecuteAsync(context);
            return;
        }
        if (!reference.StartsWith("r2:", StringComparison.Ordinal) || s3 is null ||
            !Regex.IsMatch(reference[3..], R2CoverPattern)) { context.Response.StatusCode = 404; return; }
        try
        {
            using var obj = await s3.GetObjectAsync(bucket, reference[3..], context.RequestAborted);
            context.Response.ContentType = contentType;
            context.Response.ContentLength = obj.ContentLength;
            if (HttpMethods.IsHead(context.Request.Method)) return;
            await obj.ResponseStream.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        { if (!context.Response.HasStarted) context.Response.StatusCode = 404; }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi lấy ảnh bìa từ R2");
            if (!context.Response.HasStarted) context.Response.StatusCode = 502;
        }
    }

    public async Task<string> MigrateCoverAsync(string reference, string prefix, CancellationToken ct)
    {
        if (!IsR2 || !reference.StartsWith("cover-local:", StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ chuyển được ảnh bìa Local sang R2.");
        var name = reference["cover-local:".Length..];
        if (!Regex.IsMatch(name, @"^cover-[a-f0-9]{32}\.(png|jpg|webp)$"))
            throw new InvalidOperationException("Tên file ảnh không hợp lệ.");
        var source = Path.Combine(database.DataRoot, "covers-private", name);
        if (!File.Exists(source)) throw new FileNotFoundException("Không tìm thấy ảnh Local.");
        return await SaveCoverAsync(prefix, source, Path.GetExtension(name), ct);
    }

    private async Task PutAsync(string key, string localPath, string contentType, CancellationToken ct)
    {
        if (s3 is null) throw new InvalidOperationException("Cloudflare R2 chưa được cấu hình.");
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            FilePath = localPath,
            ContentType = contentType,
            UseChunkEncoding = false
        }, ct);
    }

    /// <summary>Store encrypted HLS with unique object prefix, preventing concurrent replacement overwrites.</summary>
    public async Task<string> SaveHlsAsync(string folder, string storyPrefix, int episodeNumber, CancellationToken ct)
    {
        CheckPrefix(storyPrefix);
        if (episodeNumber is < 1 or > 100000) throw new InvalidDataException("Số tập không hợp lệ.");
        var files = Directory.GetFiles(folder).Where(f=>IsHlsFile(Path.GetFileName(f))).ToArray();
        if (!files.Any(f=>Path.GetFileName(f)=="index.m3u8") || !files.Any(f=>Path.GetFileName(f)=="key.bin") ||
            !files.Any(f=>Path.GetExtension(f)==".ts")) throw new InvalidDataException("Thiếu dữ liệu HLS.");
        var guid = Guid.NewGuid().ToString("N");
        if (!IsR2)
        {
            var dest=Path.Combine(database.DataRoot,"hls-private",guid);
            Directory.CreateDirectory(dest);
            try { foreach (var file in files) File.Copy(file, Path.Combine(dest,Path.GetFileName(file))); }
            catch { Directory.Delete(dest,true); throw; }
            return "hls-local:"+guid;
        }
        var prefix=$"{storyPrefix}/HLS/Tap-{episodeNumber:D6}-{guid}";
        try
        {
            // Limit concurrency and put playlist last: readers never see an incomplete publication.
            using var gate=new SemaphoreSlim(8);
            var tasks=files.Where(f=>Path.GetFileName(f)!="index.m3u8").Select(async file=>
            {
                await gate.WaitAsync(ct);
                try { await PutAsync(prefix+"/"+Path.GetFileName(file),file,
                    Path.GetExtension(file)==".ts" ? "video/mp2t" : "application/octet-stream",ct); }
                finally { gate.Release(); }
            });
            await Task.WhenAll(tasks);
            await PutAsync(prefix+"/index.m3u8",Path.Combine(folder,"index.m3u8"),"application/vnd.apple.mpegurl",ct);
            return "r2:"+prefix;
        }
        catch { await DeleteHlsPrefixBestEffortAsync("r2:"+prefix); throw; }
    }

    public async Task MaterializeSourceAudioAsync(string audioReference, string destination, CancellationToken ct)
    {
        if (!audioReference.StartsWith("r2:",StringComparison.Ordinal))
        {
            if (!IsSafeLocalName(audioReference)) throw new InvalidDataException("Đường dẫn MP3 không hợp lệ.");
            File.Copy(Path.Combine(database.AudioRoot,audioReference),destination);
            return;
        }
        if (s3 is null || !Regex.IsMatch(audioReference[3..],R2AudioPattern))
            throw new InvalidDataException("Không tìm thấy MP3 gốc trên R2.");
        using var response=await s3.GetObjectAsync(bucket,audioReference[3..],ct);
        await using var output=File.Create(destination);
        await response.ResponseStream.CopyToAsync(output,ct);
    }

    public async Task StreamHlsPartAsync(HttpContext ctx, string hlsReference, string fileName)
    {
        ctx.Response.Headers.CacheControl="private, no-store, max-age=0";
        ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
        if (!IsHlsRef(hlsReference) || !IsHlsFile(fileName)) { ctx.Response.StatusCode=404; return; }
        ctx.Response.ContentType=fileName.EndsWith(".m3u8",StringComparison.Ordinal) ? "application/vnd.apple.mpegurl" :
            fileName.EndsWith(".ts",StringComparison.Ordinal) ? "video/mp2t" : "application/octet-stream";
        if (hlsReference.StartsWith("hls-local:",StringComparison.Ordinal))
        {
            var path=Path.Combine(database.DataRoot,"hls-private",hlsReference["hls-local:".Length..],fileName);
            if (!File.Exists(path)) { ctx.Response.StatusCode=404; return; }
            await Results.File(path: path,contentType: ctx.Response.ContentType).ExecuteAsync(ctx);
            return;
        }
        if (s3 is null) { ctx.Response.StatusCode=503; return; }
        try
        {
            using var obj=await s3.GetObjectAsync(bucket,hlsReference[3..]+"/"+fileName,ctx.RequestAborted);
            ctx.Response.ContentLength=obj.ContentLength;
            await obj.ResponseStream.CopyToAsync(ctx.Response.Body,ctx.RequestAborted);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode==HttpStatusCode.NotFound || ex.ErrorCode=="NoSuchKey")
        { if (!ctx.Response.HasStarted) ctx.Response.StatusCode=404; }
        catch (OperationCanceledException) when(ctx.RequestAborted.IsCancellationRequested) { }
        catch(Exception ex)
        {
            logger.LogError(ex,"Không tải được HLS part {Part}",fileName);
            if (!ctx.Response.HasStarted) ctx.Response.StatusCode=502;
        }
    }

    private async Task DeleteHlsPrefixBestEffortAsync(string reference)
    {
        try { await DeleteHlsPrefixStrictAsync(reference,CancellationToken.None); }
        catch(Exception ex) { logger.LogWarning(ex,"Không dọn được HLS chưa sử dụng {Ref}",reference); }
    }
    private async Task DeleteHlsPrefixStrictAsync(string reference,CancellationToken ct)
    {
        if (!IsHlsRef(reference)) throw new InvalidDataException("Đường dẫn HLS không hợp lệ.");
        if (reference.StartsWith("hls-local:",StringComparison.Ordinal))
        {
            var path=Path.Combine(database.DataRoot,"hls-private",reference["hls-local:".Length..]);
            if (Directory.Exists(path)) Directory.Delete(path,true);
            return;
        }
        if (s3 is null) throw new InvalidOperationException("Phải bật R2 để xóa HLS.");
        string? marker=null;
        do
        {
            var page=await s3.ListObjectsV2Async(new ListObjectsV2Request {
                BucketName=bucket,Prefix=reference[3..]+"/",ContinuationToken=marker },ct);
            foreach(var item in page.S3Objects)
                await s3.DeleteObjectAsync(new DeleteObjectRequest {BucketName=bucket,Key=item.Key},ct);
            marker=page.IsTruncated ? page.NextContinuationToken : null;
        } while(marker is not null);
    }

    // Strict deletion used by the durable cleanup queue. Errors MUST surface for retry.
    public async Task DeleteStrictAsync(string reference, CancellationToken ct = default)
    {
        if (IsHlsRef(reference)) { await DeleteHlsPrefixStrictAsync(reference,ct); return; }
        if (reference.StartsWith("r2:", StringComparison.Ordinal))
        {
            if (!Regex.IsMatch(reference[3..], R2AudioPattern) &&
                !Regex.IsMatch(reference[3..], R2CoverPattern))
                throw new InvalidDataException("Đường dẫn R2 không hợp lệ.");
            if (s3 is null) throw new InvalidOperationException("Cần bật R2 để xóa object cũ.");
            await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = reference[3..] }, ct);
            return;
        }
        if (reference.StartsWith("cover-local:", StringComparison.Ordinal))
        {
            var name = reference["cover-local:".Length..];
            if (!Regex.IsMatch(name, @"^cover-[a-f0-9]{32}\.(png|jpg|webp)$"))
                throw new InvalidDataException("Đường dẫn ảnh Local không hợp lệ.");
            File.Delete(Path.Combine(database.DataRoot, "covers-private", name));
            return;
        }
        if (!IsSafeLocalName(reference)) throw new InvalidDataException("Đường dẫn audio Local không hợp lệ.");
        File.Delete(Path.Combine(database.AudioRoot, reference));
    }

    public async Task DeleteBestEffortAsync(params string[] references)
    {
        foreach (var reference in references)
        {
            try
            {
                if (IsHlsRef(reference)) { await DeleteHlsPrefixStrictAsync(reference,CancellationToken.None); continue; }
                if (reference.StartsWith("r2:", StringComparison.Ordinal))
                {
                    if (s3 is not null && (Regex.IsMatch(reference[3..], R2AudioPattern) ||
                        Regex.IsMatch(reference[3..], R2CoverPattern)))
                        await s3.DeleteObjectAsync(bucket, reference[3..]);
                }
                else if (reference.StartsWith("cover-local:", StringComparison.Ordinal) &&
                    Regex.IsMatch(reference["cover-local:".Length..], @"^cover-[a-f0-9]{32}\.(png|jpg|webp)$"))
                {
                    var path = Path.Combine(database.DataRoot, "covers-private", reference["cover-local:".Length..]);
                    if (File.Exists(path)) File.Delete(path);
                }
                else if (IsSafeLocalName(reference))
                {
                    var path = Path.Combine(database.AudioRoot, reference);
                    if (File.Exists(path)) File.Delete(path);
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "Không thể dọn file audio sau khi upload thất bại."); }
        }
    }

    public static bool IsSafeLocalName(string fileName) => !string.IsNullOrWhiteSpace(fileName) &&
        Path.GetFileName(fileName) == fileName && fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

    public async Task StreamAsync(HttpContext context, string reference)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Accept-Ranges"] = "bytes";
        if (!reference.StartsWith("r2:", StringComparison.Ordinal))
        {
            if (!IsSafeLocalName(reference)) { context.Response.StatusCode = 404; return; }
            var path = Path.Combine(database.AudioRoot, reference);
            if (!File.Exists(path)) { context.Response.StatusCode = 404; return; }
            await Results.File(path: path, contentType: "audio/mpeg", enableRangeProcessing: true).ExecuteAsync(context);
            return;
        }
        if (s3 is null) { context.Response.StatusCode = 503; return; }
        var key = reference[3..];
        if (!Regex.IsMatch(key, R2AudioPattern))
        { context.Response.StatusCode = 404; return; }
        try
        {
            // Metadata first allows RFC 7233 single-range and suffix-range requests,
            // without downloading the entire object to the ASP.NET server.
            var meta = await s3.GetObjectMetadataAsync(bucket, key, context.RequestAborted);
            var length = meta.ContentLength;
            if (length <= 0) { context.Response.StatusCode = 404; return; }
            var start = 0L;
            var end = length - 1;
            var hasRange = !string.IsNullOrWhiteSpace(context.Request.Headers.Range.ToString());
            if (hasRange)
            {
                var match = Regex.Match(context.Request.Headers.Range.ToString(), @"^bytes=(\d*)-(\d*)$", RegexOptions.CultureInvariant);
                if (!match.Success || (match.Groups[1].Length == 0 && match.Groups[2].Length == 0))
                { await InvalidRange(context, length); return; }
                if (match.Groups[1].Length > 0)
                {
                    if (!long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out start))
                    { await InvalidRange(context, length); return; }
                    if (match.Groups[2].Length > 0 &&
                        !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out end))
                    { await InvalidRange(context, length); return; }
                    if (match.Groups[2].Length == 0) end = length - 1;
                }
                else
                {
                    if (!long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var suffix))
                    { await InvalidRange(context, length); return; }
                    start = suffix >= length ? 0 : length - suffix;
                    end = length - 1;
                }
                if (start < 0 || end < start || start >= length)
                { await InvalidRange(context, length); return; }
                end = Math.Min(end, length - 1);
                context.Response.StatusCode = StatusCodes.Status206PartialContent;
                context.Response.Headers.ContentRange = $"bytes {start}-{end}/{length}";
            }
            context.Response.ContentType = "audio/mpeg";
            context.Response.ContentLength = end - start + 1;
            if (HttpMethods.IsHead(context.Request.Method)) return;
            using var obj = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket, Key = key,
                ByteRange = hasRange ? new ByteRange(start, end) : null
            }, context.RequestAborted);
            await obj.ResponseStream.CopyToAsync(context.Response.Body, 64 * 1024, context.RequestAborted);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        { if (!context.Response.HasStarted) context.Response.StatusCode = 404; }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không tải được audio từ R2 (object: {Key})", key);
            if (!context.Response.HasStarted) context.Response.StatusCode = 502;
        }
    }

    private static Task InvalidRange(HttpContext ctx, long total)
    {
        ctx.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
        ctx.Response.Headers.ContentRange = $"bytes */{total}";
        ctx.Response.ContentLength = 0;
        return Task.CompletedTask;
    }

    public void Dispose() => s3?.Dispose();
}
