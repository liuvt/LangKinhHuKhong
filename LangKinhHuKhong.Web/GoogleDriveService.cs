using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;

namespace LangKinhHuKhong;

public sealed class GoogleDriveService
{
    private readonly Database database;
    private readonly IWebHostEnvironment environment;
    private readonly IHttpClientFactory httpFactory;
    private readonly IDataProtector protector;
    private readonly ILogger<GoogleDriveService> logger;
    private readonly SemaphoreSlim lockGate = new(1, 1);

    private string? cachedAccessToken;
    private DateTimeOffset tokenExpiresAt = DateTimeOffset.MinValue;

    public const string DefaultRootFolderName = "LangKinhHuKhong-BanThao";

    public GoogleDriveService(
        Database database,
        IWebHostEnvironment environment,
        IHttpClientFactory httpFactory,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<GoogleDriveService> logger)
    {
        this.database = database;
        this.environment = environment;
        this.httpFactory = httpFactory;
        this.protector = dataProtectionProvider.CreateProtector("LangKinhHuKhong.GoogleDrive.OAuth.v1");
        this.logger = logger;
        TryBootstrapFromDownloadedSecretFile();
    }

    private string ConfigPath => Path.Combine(database.DataRoot, "gdrive-oauth.json");

    private sealed class StoredConfig
    {
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string ProtectedRefreshToken { get; set; } = "";
        public string PlainRefreshTokenFallback { get; set; } = "";
        public string ConnectedEmail { get; set; } = "";
        public string ConnectedName { get; set; } = "";
        public string RootFolderId { get; set; } = "";
        public string RootFolderName { get; set; } = DefaultRootFolderName;
        public string UpdatedAt { get; set; } = "";
    }

    private void TryBootstrapFromDownloadedSecretFile()
    {
        try
        {
            var existing = LoadConfigInternal();
            if (!string.IsNullOrWhiteSpace(existing.ClientId) && !string.IsNullOrWhiteSpace(existing.ClientSecret))
                return;

            var searchDirs = new[]
            {
                database.DataRoot,
                environment.ContentRootPath,
                Directory.GetParent(environment.ContentRootPath)?.FullName ?? ""
            }.Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d)).Distinct();

            foreach (var dir in searchDirs)
            {
                var file = Directory.GetFiles(dir, "client_secret*.json").FirstOrDefault();
                if (file is null) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                JsonElement section = default;
                if (root.TryGetProperty("web", out var web)) section = web;
                else if (root.TryGetProperty("installed", out var inst)) section = inst;
                if (section.ValueKind != JsonValueKind.Object) continue;

                var cid = section.TryGetProperty("client_id", out var cEl) ? cEl.GetString() ?? "" : "";
                var csec = section.TryGetProperty("client_secret", out var sEl) ? sEl.GetString() ?? "" : "";
                if (!string.IsNullOrWhiteSpace(cid) && !string.IsNullOrWhiteSpace(csec))
                {
                    existing.ClientId = cid.Trim();
                    existing.ClientSecret = csec.Trim();
                    existing.UpdatedAt = Helpers.Now();
                    SaveConfigInternal(existing);
                    logger.LogInformation("Đã nạp cấu hình Google OAuth Client từ {File}", Path.GetFileName(file));
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể tự động nạp file client_secret Google OAuth.");
        }
    }

    private StoredConfig LoadConfigInternal()
    {
        if (!File.Exists(ConfigPath)) return new StoredConfig();
        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<StoredConfig>(json) ?? new StoredConfig();
        }
        catch
        {
            return new StoredConfig();
        }
    }

    private void SaveConfigInternal(StoredConfig cfg)
    {
        Directory.CreateDirectory(database.DataRoot);
        var tmp = ConfigPath + ".tmp";
        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(tmp, json, new UTF8Encoding(false));
        File.Move(tmp, ConfigPath, overwrite: true);
    }

    private string? GetRefreshToken(StoredConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.ProtectedRefreshToken))
        {
            try { return protector.Unprotect(cfg.ProtectedRefreshToken); }
            catch { /* Fallback if DataProtection key ring changed */ }
        }
        return string.IsNullOrWhiteSpace(cfg.PlainRefreshTokenFallback) ? null : cfg.PlainRefreshTokenFallback;
    }

    public object GetStatus()
    {
        var cfg = LoadConfigInternal();
        var hasClient = !string.IsNullOrWhiteSpace(cfg.ClientId) && !string.IsNullOrWhiteSpace(cfg.ClientSecret);
        var refresh = GetRefreshToken(cfg);
        var connected = hasClient && !string.IsNullOrWhiteSpace(refresh);
        var preview = cfg.ClientId.Length > 18 ? cfg.ClientId[..18] + "..." : cfg.ClientId;
        return new
        {
            configured = hasClient,
            connected,
            clientId = cfg.ClientId,
            clientIdPreview = preview,
            email = cfg.ConnectedEmail,
            displayName = cfg.ConnectedName,
            rootFolderId = cfg.RootFolderId,
            rootFolderName = string.IsNullOrWhiteSpace(cfg.RootFolderName) ? DefaultRootFolderName : cfg.RootFolderName,
            rootFolderUrl = !string.IsNullOrWhiteSpace(cfg.RootFolderId)
                ? $"https://drive.google.com/drive/folders/{cfg.RootFolderId}"
                : "",
            updatedAt = cfg.UpdatedAt
        };
    }

    public async Task SaveCredentialsConfigAsync(string? clientId, string? clientSecret, string? rootFolderId, string? rootFolderName)
    {
        await lockGate.WaitAsync();
        try
        {
            var cfg = LoadConfigInternal();
            if (!string.IsNullOrWhiteSpace(clientId)) cfg.ClientId = clientId.Trim();
            if (!string.IsNullOrWhiteSpace(clientSecret)) cfg.ClientSecret = clientSecret.Trim();
            if (rootFolderId is not null)
            {
                var cleaned = ExtractFolderId(rootFolderId);
                cfg.RootFolderId = cleaned;
            }
            if (!string.IsNullOrWhiteSpace(rootFolderName))
                cfg.RootFolderName = rootFolderName.Trim();
            cfg.UpdatedAt = Helpers.Now();
            cachedAccessToken = null;
            SaveConfigInternal(cfg);
        }
        finally
        {
            lockGate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await lockGate.WaitAsync();
        try
        {
            var cfg = LoadConfigInternal();
            cfg.ProtectedRefreshToken = "";
            cfg.PlainRefreshTokenFallback = "";
            cfg.ConnectedEmail = "";
            cfg.ConnectedName = "";
            cfg.UpdatedAt = Helpers.Now();
            cachedAccessToken = null;
            SaveConfigInternal(cfg);
        }
        finally
        {
            lockGate.Release();
        }
    }

    public static string ExtractFolderId(string input)
    {
        var s = (input ?? "").Trim();
        if (s == "") return "";
        var idx = s.IndexOf("/folders/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            s = s[(idx + "/folders/".Length)..];
            var q = s.IndexOfAny(['?', '/', '&', '#']);
            if (q >= 0) s = s[..q];
        }
        return s.Trim();
    }

    public string BuildAuthorizeUrl(string redirectUri, string state)
    {
        var cfg = LoadConfigInternal();
        if (string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
            throw new InvalidOperationException("Chưa cấu hình Google OAuth Client ID và Client Secret.");

        var query = new Dictionary<string, string>
        {
            ["client_id"] = cfg.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "https://www.googleapis.com/auth/drive",
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true",
            ["state"] = state
        };
        var qs = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return "https://accounts.google.com/o/oauth2/v2/auth?" + qs;
    }

    public async Task ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        var cfg = LoadConfigInternal();
        if (string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
            throw new InvalidOperationException("Chưa cấu hình Google OAuth Client ID / Secret.");

        var client = httpFactory.CreateClient();
        using var tokenRes = await client.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = cfg.ClientId,
                ["client_secret"] = cfg.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            }), ct);

        var body = await tokenRes.Content.ReadAsStringAsync(ct);
        if (!tokenRes.IsSuccessStatusCode)
            throw new InvalidOperationException("Google OAuth từ chối mã xác thực: " + body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() ?? "" : "";
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
        var expiresIn = root.TryGetProperty("expires_in", out var ex) && ex.TryGetInt32(out var sec) ? sec : 3500;

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Google không trả về access_token.");

        await lockGate.WaitAsync(ct);
        try
        {
            cfg = LoadConfigInternal();
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                cfg.ProtectedRefreshToken = protector.Protect(refreshToken);
                cfg.PlainRefreshTokenFallback = refreshToken;
            }
            else if (string.IsNullOrWhiteSpace(GetRefreshToken(cfg)))
            {
                throw new InvalidOperationException("Google không trả về refresh_token. Hãy thử kết nối lại và chấp thuận quyền.");
            }

            cachedAccessToken = accessToken;
            tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 120));

            // Query user profile from Drive About API
            using var aboutReq = new HttpRequestMessage(HttpMethod.Get,
                "https://www.googleapis.com/drive/v3/about?fields=user(emailAddress,displayName)");
            aboutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var aboutRes = await client.SendAsync(aboutReq, ct);
            if (aboutRes.IsSuccessStatusCode)
            {
                using var aboutDoc = JsonDocument.Parse(await aboutRes.Content.ReadAsStringAsync(ct));
                if (aboutDoc.RootElement.TryGetProperty("user", out var u))
                {
                    cfg.ConnectedEmail = u.TryGetProperty("emailAddress", out var em) ? em.GetString() ?? "" : "";
                    cfg.ConnectedName = u.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
                }
            }

            cfg.UpdatedAt = Helpers.Now();
            SaveConfigInternal(cfg);
        }
        finally
        {
            lockGate.Release();
        }

        // Ensure default root folder exists on Drive
        await EnsureRootFolderAsync(ct);
    }

    public bool IsConnected
    {
        get
        {
            var cfg = LoadConfigInternal();
            return !string.IsNullOrWhiteSpace(cfg.ClientId) &&
                   !string.IsNullOrWhiteSpace(cfg.ClientSecret) &&
                   !string.IsNullOrWhiteSpace(GetRefreshToken(cfg));
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(cachedAccessToken) && DateTimeOffset.UtcNow < tokenExpiresAt)
            return cachedAccessToken;

        await lockGate.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(cachedAccessToken) && DateTimeOffset.UtcNow < tokenExpiresAt)
                return cachedAccessToken;

            var cfg = LoadConfigInternal();
            var refresh = GetRefreshToken(cfg);
            if (string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(cfg.ClientSecret) || string.IsNullOrWhiteSpace(refresh))
                throw new InvalidOperationException("Chưa kết nối tài khoản Google Drive.");

            var client = httpFactory.CreateClient();
            using var res = await client.PostAsync("https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = cfg.ClientId,
                    ["client_secret"] = cfg.ClientSecret,
                    ["refresh_token"] = refresh,
                    ["grant_type"] = "refresh_token"
                }), ct);

            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException("Không làm mới được phiên Google Drive (hãy bấm Kết nối lại): " + body);

            using var doc = JsonDocument.Parse(body);
            var token = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() ?? "" : "";
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ex) && ex.TryGetInt32(out var sec) ? sec : 3500;
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Google không trả về access_token khi làm mới.");

            cachedAccessToken = token;
            tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 120));
            return token;
        }
        finally
        {
            lockGate.Release();
        }
    }

    private static string EscapeDriveQuery(string value)
        => (value ?? "").Replace("\\", "\\\\").Replace("'", "\\'");

    public async Task<string> EnsureRootFolderAsync(CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var cfg = LoadConfigInternal();
        var rootName = string.IsNullOrWhiteSpace(cfg.RootFolderName) ? DefaultRootFolderName : cfg.RootFolderName.Trim();

        if (!string.IsNullOrWhiteSpace(cfg.RootFolderId))
        {
            var meta = await TryGetFileMetaAsync(token, cfg.RootFolderId, ct);
            if (meta is not null && !meta.Value.Trashed)
                return cfg.RootFolderId;
        }

        // Search existing folder by name in root
        var q = $"mimeType='application/vnd.google-apps.folder' and name='{EscapeDriveQuery(rootName)}' and trashed=false";
        var foundId = await FindFirstFileIdAsync(token, q, ct);
        if (string.IsNullOrWhiteSpace(foundId))
            foundId = await CreateFolderInternalAsync(token, rootName, null, ct);

        await lockGate.WaitAsync(ct);
        try
        {
            cfg = LoadConfigInternal();
            cfg.RootFolderId = foundId;
            cfg.RootFolderName = rootName;
            cfg.UpdatedAt = Helpers.Now();
            SaveConfigInternal(cfg);
        }
        finally
        {
            lockGate.Release();
        }
        return foundId;
    }

    public async Task<string> EnsureStoryFolderAsync(SqliteConnection db, string storyId, CancellationToken ct)
    {
        var story = await db.FirstAsync("SELECT id,title,slug,drive_folder_id FROM stories WHERE id=@id", ("@id", storyId))
            ?? throw new InvalidOperationException("Không tìm thấy bộ truyện.");

        var token = await GetAccessTokenAsync(ct);
        var rootFolderId = await EnsureRootFolderAsync(ct);
        var folderTitle = $"{story.S("title")} [{story.S("slug")}]";
        var existingFolderId = story.S("drive_folder_id");

        if (!string.IsNullOrWhiteSpace(existingFolderId))
        {
            var meta = await TryGetFileMetaAsync(token, existingFolderId, ct);
            if (meta is not null && !meta.Value.Trashed)
                return existingFolderId;
        }

        var q = $"mimeType='application/vnd.google-apps.folder' and '{EscapeDriveQuery(rootFolderId)}' in parents and name='{EscapeDriveQuery(folderTitle)}' and trashed=false";
        var folderId = await FindFirstFileIdAsync(token, q, ct);
        if (string.IsNullOrWhiteSpace(folderId))
            folderId = await CreateFolderInternalAsync(token, folderTitle, rootFolderId, ct);

        await db.ExecAsync("UPDATE stories SET drive_folder_id=@fid WHERE id=@id", ("@fid", folderId), ("@id", storyId));
        return folderId;
    }

    private async Task<(string Id, string Name, bool Trashed)?> TryGetFileMetaAsync(string token, string fileId, CancellationToken ct)
    {
        var client = httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}?fields=id,name,trashed");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await client.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var r = doc.RootElement;
        return (
            r.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
            r.TryGetProperty("trashed", out var tr) && tr.ValueKind == JsonValueKind.True
        );
    }

    private async Task<string> FindFirstFileIdAsync(string token, string query, CancellationToken ct)
    {
        var client = httpFactory.CreateClient();
        var url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&pageSize=1&fields=files(id,name)";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return "";
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            var first = files.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("id", out var id))
                return id.GetString() ?? "";
        }
        return "";
    }

    private async Task<string> CreateFolderInternalAsync(string token, string name, string? parentId, CancellationToken ct)
    {
        var client = httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files?fields=id,name");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        object payload = string.IsNullOrWhiteSpace(parentId)
            ? new { name, mimeType = "application/vnd.google-apps.folder" }
            : new { name, mimeType = "application/vnd.google-apps.folder", parents = new[] { parentId } };
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("Không tạo được thư mục trên Google Drive: " + body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetString() ?? "";
    }

    public async Task<string> UpsertTextFileOnDriveAsync(
        string folderId, string existingDriveFileId, string fileName, string titleDescription, byte[] contentBytes, string extension, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var mimeType = extension == ".md" ? "text/markdown; charset=utf-8" : "text/plain; charset=utf-8";

        if (!string.IsNullOrWhiteSpace(existingDriveFileId))
        {
            var meta = await TryGetFileMetaAsync(token, existingDriveFileId, ct);
            if (meta is not null && !meta.Value.Trashed)
            {
                return await UploadMultipartAsync(token, HttpMethod.Patch,
                    $"https://www.googleapis.com/upload/drive/v3/files/{Uri.EscapeDataString(existingDriveFileId)}?uploadType=multipart&fields=id,name",
                    new { name = fileName, description = titleDescription }, contentBytes, mimeType, ct);
            }
        }

        // Check if a file with the same name already exists in folderId
        var q = $"'{EscapeDriveQuery(folderId)}' in parents and name='{EscapeDriveQuery(fileName)}' and trashed=false and mimeType!='application/vnd.google-apps.folder'";
        var sameNameId = await FindFirstFileIdAsync(token, q, ct);
        if (!string.IsNullOrWhiteSpace(sameNameId))
        {
            return await UploadMultipartAsync(token, HttpMethod.Patch,
                $"https://www.googleapis.com/upload/drive/v3/files/{Uri.EscapeDataString(sameNameId)}?uploadType=multipart&fields=id,name",
                new { name = fileName, description = titleDescription }, contentBytes, mimeType, ct);
        }

        return await UploadMultipartAsync(token, HttpMethod.Post,
            "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name",
            new { name = fileName, description = titleDescription, parents = new[] { folderId } }, contentBytes, mimeType, ct);
    }

    private async Task<string> UploadMultipartAsync(
        string token, HttpMethod method, string url, object metadata, byte[] fileBytes, string contentType, CancellationToken ct)
    {
        var boundary = "lkh_gdrive_" + Guid.NewGuid().ToString("N");
        var metaJson = JsonSerializer.Serialize(metadata);
        using var bodyStream = new MemoryStream();
        var utf8NoBom = new UTF8Encoding(false);

        var headerPart =
            $"--{boundary}\r\n" +
            "Content-Type: application/json; charset=UTF-8\r\n\r\n" +
            $"{metaJson}\r\n" +
            $"--{boundary}\r\n" +
            $"Content-Type: {contentType}\r\n\r\n";
        var footerPart = $"\r\n--{boundary}--\r\n";

        var headerBytes = utf8NoBom.GetBytes(headerPart);
        var footerBytes = utf8NoBom.GetBytes(footerPart);
        await bodyStream.WriteAsync(headerBytes, ct);
        await bodyStream.WriteAsync(fileBytes, ct);
        await bodyStream.WriteAsync(footerBytes, ct);
        bodyStream.Position = 0;

        var client = httpFactory.CreateClient();
        using var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var streamContent = new StreamContent(bodyStream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/related; boundary={boundary}");
        req.Content = streamContent;

        using var res = await client.SendAsync(req, ct);
        var respText = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("Google Drive upload thất bại: " + respText);

        using var doc = JsonDocument.Parse(respText);
        return doc.RootElement.GetProperty("id").GetString() ?? "";
    }

    public async Task DeleteDriveItemAsync(string driveFileId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(driveFileId)) return;
        var token = await GetAccessTokenAsync(ct);
        var client = httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Patch,
            $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(driveFileId)}?fields=id,trashed");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent("{\"trashed\":true}", Encoding.UTF8, "application/json");
        using var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode && res.StatusCode != HttpStatusCode.NotFound)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException("Không thể xóa file trên Google Drive: " + err);
        }
    }

    public async Task<object> ListFolderItemsAsync(string? folderId, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var rootId = await EnsureRootFolderAsync(ct);
        var targetFolder = string.IsNullOrWhiteSpace(folderId) ? rootId : ExtractFolderId(folderId);

        var client = httpFactory.CreateClient();
        var q = $"'{EscapeDriveQuery(targetFolder)}' in parents and trashed=false";
        var url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(q)}&pageSize=200&orderBy=folder,name&fields=files(id,name,mimeType,size,modifiedTime,description,webViewLink)";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("Không đọc được danh sách Google Drive: " + body);

        using var doc = JsonDocument.Parse(body);
        var items = new List<object>();
        if (doc.RootElement.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in files.EnumerateArray())
            {
                var mime = f.TryGetProperty("mimeType", out var m) ? m.GetString() ?? "" : "";
                var isFolder = mime == "application/vnd.google-apps.folder";
                var name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var size = f.TryGetProperty("size", out var sz) && long.TryParse(sz.GetString(), out var parsedSize) ? (int)Math.Min(int.MaxValue, parsedSize) : 0;
                items.Add(new
                {
                    id = f.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    name,
                    mimeType = mime,
                    isFolder,
                    isEditableText = !isFolder && (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                                                   name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                                                   mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase)),
                    sizeBytes = size,
                    description = f.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                    modifiedTime = f.TryGetProperty("modifiedTime", out var mt) ? mt.GetString() ?? "" : "",
                    webViewLink = f.TryGetProperty("webViewLink", out var wv) ? wv.GetString() ?? "" : ""
                });
            }
        }

        return new
        {
            folderId = targetFolder,
            rootFolderId = rootId,
            isRoot = string.Equals(targetFolder, rootId, StringComparison.Ordinal),
            folderUrl = $"https://drive.google.com/drive/folders/{targetFolder}",
            items
        };
    }

    public async Task<string> CreateSubfolderOnDriveAsync(string? parentFolderId, string folderName, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var rootId = await EnsureRootFolderAsync(ct);
        var targetParent = string.IsNullOrWhiteSpace(parentFolderId) ? rootId : ExtractFolderId(parentFolderId);
        return await CreateFolderInternalAsync(token, folderName.Trim(), targetParent, ct);
    }

    public async Task<(string Id, string Name, string Description, int SizeBytes, string ModifiedTime, string WebViewLink, string Content)> ReadDriveFileTextAsync(
        string driveFileId, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var client = httpFactory.CreateClient();

        using var metaReq = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(driveFileId)}?fields=id,name,mimeType,size,modifiedTime,description,webViewLink");
        metaReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var metaRes = await client.SendAsync(metaReq, ct);
        var metaBody = await metaRes.Content.ReadAsStringAsync(ct);
        if (!metaRes.IsSuccessStatusCode)
            throw new InvalidOperationException("Không đọc được thông tin file trên Google Drive: " + metaBody);

        using var metaDoc = JsonDocument.Parse(metaBody);
        var r = metaDoc.RootElement;
        var name = r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var mime = r.TryGetProperty("mimeType", out var m) ? m.GetString() ?? "" : "";
        var desc = r.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        var mod = r.TryGetProperty("modifiedTime", out var mt) ? mt.GetString() ?? "" : "";
        var link = r.TryGetProperty("webViewLink", out var wv) ? wv.GetString() ?? "" : "";
        var size = r.TryGetProperty("size", out var sz) && int.TryParse(sz.GetString(), out var sVal) ? sVal : 0;

        var downloadUrl = mime == "application/vnd.google-apps.document"
            ? $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(driveFileId)}/export?mimeType=text/plain"
            : $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(driveFileId)}?alt=media";

        using var contentReq = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        contentReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var contentRes = await client.SendAsync(contentReq, ct);
        if (!contentRes.IsSuccessStatusCode)
        {
            var err = await contentRes.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException("Không tải được nội dung file từ Google Drive: " + err);
        }

        var bytes = await contentRes.Content.ReadAsByteArrayAsync(ct);
        using var ms = new MemoryStream(bytes);
        using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(ct);
        return (driveFileId, name, desc, bytes.Length > 0 ? bytes.Length : size, mod, link, content);
    }

    public async Task<byte[]> DownloadDriveFileBytesAsync(string driveFileId, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var client = httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(driveFileId)}?alt=media");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException("Không tải được file từ Google Drive: " + err);
        }
        return await res.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<(string Id, string Name, int SizeBytes)> SaveDriveFileDirectAsync(
        string? driveFileId, string? parentFolderId, string fileName, string description, string content, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var rootId = await EnsureRootFolderAsync(ct);
        var targetParent = string.IsNullOrWhiteSpace(parentFolderId) ? rootId : ExtractFolderId(parentFolderId);

        var cleanName = Path.GetFileName((fileName ?? "").Trim());
        if (string.IsNullOrWhiteSpace(cleanName))
            cleanName = "ban-thao-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt";
        var ext = Path.GetExtension(cleanName).ToLowerInvariant();
        if (ext is not (".md" or ".txt"))
        {
            ext = ".txt";
            cleanName += ".txt";
        }

        var utf8NoBom = new UTF8Encoding(false);
        var bytes = utf8NoBom.GetBytes(content ?? "");
        var savedId = await UpsertTextFileOnDriveAsync(targetParent, driveFileId ?? "", cleanName, description ?? "", bytes, ext, ct);

        await using var db = await database.OpenAsync();
        var linked = await db.FirstAsync("SELECT id,story_id,stored_name,storage_target FROM story_text_files WHERE drive_file_id=@did", ("@did", savedId));
        if (linked is not null)
        {
            if (!string.IsNullOrWhiteSpace(linked.S("stored_name")) && linked.S("storage_target") != "drive")
            {
                var localPath = Path.Combine(database.TextRoot, linked.S("story_id"), linked.S("stored_name"));
                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                await File.WriteAllBytesAsync(localPath, bytes, ct);
            }
            await db.ExecAsync(@"UPDATE story_text_files
                SET title=@title, file_name=@fname, size_bytes=@size, updated_at=@now
                WHERE id=@id",
                ("@title", string.IsNullOrWhiteSpace(description) ? Path.GetFileNameWithoutExtension(cleanName) : description.Trim()),
                ("@fname", cleanName), ("@size", bytes.Length), ("@now", Helpers.Now()), ("@id", linked.S("id")));
        }

        return (savedId, cleanName, bytes.Length);
    }

    public async Task<(int Uploaded, int StoriesSynced)> SyncPushAllToDriveAsync(CancellationToken ct)
    {
        await using var db = await database.OpenAsync();
        var stories = await db.ListAsync("SELECT id,title,slug FROM stories ORDER BY updated_at DESC");
        var uploadedCount = 0;
        var storyCount = 0;

        foreach (var s in stories)
        {
            var sid = s.S("id");
            var folderId = await EnsureStoryFolderAsync(db, sid, ct);
            storyCount++;

            var files = await db.ListAsync("SELECT * FROM story_text_files WHERE story_id=@sid ORDER BY created_at ASC", ("@sid", sid));
            foreach (var tf in files)
            {
                var sname = tf.S("stored_name");
                if (string.IsNullOrWhiteSpace(sname)) continue;
                var localPath = Path.Combine(database.TextRoot, sid, sname);
                if (!File.Exists(localPath)) continue;
                var bytes = await File.ReadAllBytesAsync(localPath, ct);
                var driveId = await UpsertTextFileOnDriveAsync(
                    folderId,
                    tf.S("drive_file_id"),
                    tf.S("file_name"),
                    tf.S("title"),
                    bytes,
                    tf.S("extension"),
                    ct);
                if (driveId != "" && driveId != tf.S("drive_file_id"))
                {
                    await db.ExecAsync("UPDATE story_text_files SET drive_file_id=@did WHERE id=@id",
                        ("@did", driveId), ("@id", tf.S("id")));
                }
                uploadedCount++;
            }
        }
        return (uploadedCount, storyCount);
    }

    public async Task<(int Imported, int Updated)> SyncPullFromDriveAsync(CancellationToken ct)
    {
        await using var db = await database.OpenAsync();
        var stories = await db.ListAsync("SELECT id,title,slug,drive_folder_id FROM stories ORDER BY updated_at DESC");
        var imported = 0;
        var updated = 0;

        foreach (var s in stories)
        {
            var sid = s.S("id");
            var folderId = await EnsureStoryFolderAsync(db, sid, ct);
            var folderListing = await ListFolderItemsAsync(folderId, ct);
            var json = JsonSerializer.SerializeToElement(folderListing);
            if (!json.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                continue;

            var storyDir = Path.Combine(database.TextRoot, sid);
            Directory.CreateDirectory(storyDir);

            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("isFolder", out var isF) && isF.ValueKind == JsonValueKind.True)
                    continue;
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var ext = Path.GetExtension(name).ToLowerInvariant();
                if (ext is not (".md" or ".txt")) continue;

                var driveId = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(driveId)) continue;

                var (_, _, desc, _, _, _, content) = await ReadDriveFileTextAsync(driveId, ct);
                var bytes = new UTF8Encoding(false).GetBytes(content);
                var now = Helpers.Now();

                var existing = await db.FirstAsync(
                    "SELECT * FROM story_text_files WHERE story_id=@sid AND (drive_file_id=@did OR file_name=@fname) LIMIT 1",
                    ("@sid", sid), ("@did", driveId), ("@fname", name));

                if (existing is null)
                {
                    var textId = Helpers.Id();
                    var storedName = textId + ext;
                    var destPath = Path.Combine(storyDir, storedName);
                    await File.WriteAllBytesAsync(destPath, bytes, ct);
                    var title = !string.IsNullOrWhiteSpace(desc) ? desc.Trim() : Path.GetFileNameWithoutExtension(name);
                    if (title.Length > 180) title = title[..180];

                    await db.ExecAsync(@"INSERT INTO story_text_files(id,story_id,title,file_name,stored_name,extension,size_bytes,drive_file_id,created_at,updated_at)
                        VALUES(@id,@sid,@title,@fname,@sname,@ext,@size,@did,@now,@now)",
                        ("@id", textId), ("@sid", sid), ("@title", title), ("@fname", name),
                        ("@sname", storedName), ("@ext", ext), ("@size", bytes.Length), ("@did", driveId), ("@now", now));
                    imported++;
                }
                else
                {
                    var destPath = Path.Combine(storyDir, existing.S("stored_name"));
                    var localChanged = !File.Exists(destPath) || new FileInfo(destPath).Length != bytes.Length;
                    await File.WriteAllBytesAsync(destPath, bytes, ct);
                    await db.ExecAsync(@"UPDATE story_text_files
                        SET file_name=@fname, size_bytes=@size, drive_file_id=@did, updated_at=@now
                        WHERE id=@id",
                        ("@fname", name), ("@size", bytes.Length), ("@did", driveId), ("@now", now), ("@id", existing.S("id")));
                    if (localChanged) updated++;
                }
            }
        }
        return (imported, updated);
    }
}
