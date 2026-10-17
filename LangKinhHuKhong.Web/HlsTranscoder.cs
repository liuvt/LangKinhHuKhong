using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace LangKinhHuKhong;

/// <summary>Package full source MP3 into AES-128 HLS VOD once, during Admin upload / migration.
/// key.bin and *.ts are NEVER served from wwwroot. The API validates Premium for every part.</summary>
public static class HlsTranscoder
{
    public static async Task CreateAsync(string inputMp3, string targetFolder, CancellationToken ct)
    {
        Directory.CreateDirectory(targetFolder);
        var keyFile = Path.Combine(targetFolder, "key.bin");
        var keyInfo = Path.Combine(targetFolder, "keyinfo.txt");
        await File.WriteAllBytesAsync(keyFile, RandomNumberGenerator.GetBytes(16), ct);
        // URI is relative to index.m3u8. Authenticated route serves key.bin.
        // FFmpeg demands local filesystem path on the second line.
        await File.WriteAllTextAsync(keyInfo, "key.bin\n" + keyFile.Replace('\\','/') + "\n", new UTF8Encoding(false), ct);
        try
        {
            var ffmpeg = Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "ffmpeg";
            var start = new ProcessStartInfo { FileName=ffmpeg, RedirectStandardError=true,
                RedirectStandardOutput=true, UseShellExecute=false, CreateNoWindow=true };
            foreach (var arg in new[]{ "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i",inputMp3,
                     "-map","0:a:0", "-vn", "-c:a","aac","-b:a","96k", "-ar","44100",
                     "-f","hls", "-hls_time","4", "-hls_playlist_type","vod",
                     "-hls_segment_type","mpegts", "-hls_key_info_file",keyInfo,
                     "-hls_segment_filename",Path.Combine(targetFolder,"segment-%06d.ts"),
                     Path.Combine(targetFolder,"index.m3u8") }) start.ArgumentList.Add(arg);
            using var process=Process.Start(start) ?? throw new InvalidOperationException("Không chạy được FFmpeg.");
            using var abort=ct.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree:true); } catch { } });
            var stderr=process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var errors=await stderr;
            if (process.ExitCode!=0) throw new InvalidDataException("FFmpeg HLS thất bại: "+(errors.Length>400 ? errors[..400] : errors));
            var playlist=Path.Combine(targetFolder,"index.m3u8");
            if (!File.Exists(playlist) || !File.ReadAllText(playlist).Contains("URI=\"key.bin\"") ||
                !File.ReadAllText(playlist).Contains("#EXT-X-ENDLIST") ||
                Directory.GetFiles(targetFolder,"segment-*.ts").Length==0)
                throw new InvalidDataException("HLS playlist hoặc khóa không hợp lệ.");
        }
        finally { if (File.Exists(keyInfo)) File.Delete(keyInfo); }
    }
}
