using Microsoft.Data.Sqlite;

namespace LangKinhHuKhong;

/// <summary>
/// Transactionally queues obsolete object references and safely retries deletions.
/// A queued reference is removed only after its object is deleted (or it becomes active again).
/// </summary>
public sealed class StorageJanitor(Database source, AudioStorage storage, ILogger<StorageJanitor> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public static async Task QueueAsync(SqliteConnection db, SqliteTransaction tx, params string[] references)
    {
        foreach (var reference in references.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
        {
            await using var cmd = DbExtensions.Cmd(db,
                "INSERT OR IGNORE INTO storage_cleanup(reference,enqueued_at) VALUES(@ref,@at)",tx,
                ("@ref",reference),("@at",Helpers.Now()));
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // Cleanup is secondary to the successful database transaction: a temporary R2 outage
    // must never turn a completed upload/delete into a misleading HTTP failure.
    public async Task TrySweepAsync()
    {
        try { await SweepAsync(); }
        catch (Exception ex) { logger.LogWarning(ex, "Không thể chạy dọn file cũ, sẽ thử lại sau."); }
    }

    public async Task<(int deleted, int pending)> SweepAsync(CancellationToken ct = default)
    {
        // Never let concurrent requests race to delete the same file.
        await gate.WaitAsync(ct);
        try
        {
            int deleted = 0;
            await using var db = await source.OpenAsync();
            var pending = await db.ListAsync("SELECT reference FROM storage_cleanup ORDER BY enqueued_at LIMIT 50");
            foreach (var row in pending)
            {
                if (ct.IsCancellationRequested) break;
                var reference = row.S("reference");
                try
                {
                    // The database always wins: never delete an object still referenced by a story/episode.
                    var inUse = Convert.ToInt64(await db.ValueAsync(@"SELECT CASE WHEN
                          EXISTS(SELECT 1 FROM stories WHERE cover_file=@ref) OR
                          EXISTS(SELECT 1 FROM episodes WHERE full_file=@ref OR preview_file=@ref OR hls_ref=@ref)
                          THEN 1 ELSE 0 END", ("@ref",reference))) > 0;
                    if (!inUse) { await storage.DeleteStrictAsync(reference, ct); deleted++; }
                    await db.ExecAsync("DELETE FROM storage_cleanup WHERE reference=@ref", ("@ref",reference));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Không xóa được object cũ: {Reference}. Sẽ thử lại.", reference);
                    await db.ExecAsync("UPDATE storage_cleanup SET attempts=attempts+1,last_error=@error WHERE reference=@ref",
                        ("@error",ex.Message.Length>350 ? ex.Message[..350] : ex.Message), ("@ref",reference));
                }
            }
            var count = Convert.ToInt32(await db.ValueAsync("SELECT COUNT(*) FROM storage_cleanup"));
            return (deleted,count);
        }
        finally { gate.Release(); }
    }
}
