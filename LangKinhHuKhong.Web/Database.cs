using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LangKinhHuKhong;

public sealed class Database(IWebHostEnvironment environment)
{
    public string DataRoot { get; } = Path.GetFullPath(
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LKH_DATA_ROOT"))
            ? Path.Combine(environment.ContentRootPath, "data")
            : Environment.GetEnvironmentVariable("LKH_DATA_ROOT")!);
    public string AudioRoot => Path.Combine(DataRoot, "audio-private");
    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(DataRoot, "langkinhhukhong.db"),
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = true,
        DefaultTimeout = 15
    }.ToString();

    public async Task<SqliteConnection> OpenAsync()
    {
        var db = new SqliteConnection(ConnectionString);
        await db.OpenAsync();
        await using var pragma = db.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 15000;";
        await pragma.ExecuteNonQueryAsync();
        return db;
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(AudioRoot);
        await using var db = await OpenAsync();
        await using (var wal = db.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode = WAL;";
            await wal.ExecuteNonQueryAsync();
        }
        await using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = await File.ReadAllTextAsync(Path.Combine(environment.ContentRootPath, "schema.sql"));
            await cmd.ExecuteNonQueryAsync();
        }
        // Existing V1.2 SQLite databases gain the new columns without losing records.
        var hasCover = Convert.ToInt32(await db.ValueAsync("SELECT COUNT(*) FROM pragma_table_info('stories') WHERE name='cover_file'")) > 0;
        if (!hasCover) await db.ExecAsync("ALTER TABLE stories ADD COLUMN cover_file TEXT NOT NULL DEFAULT ''");
        var hasPrefix = Convert.ToInt32(await db.ValueAsync("SELECT COUNT(*) FROM pragma_table_info('stories') WHERE name='storage_prefix'")) > 0;
        if (!hasPrefix) await db.ExecAsync("ALTER TABLE stories ADD COLUMN storage_prefix TEXT NOT NULL DEFAULT ''");
        // V1.8: previous episodes remain intact and can be packaged via Admin -> Tạo HLS.
        var hasHls = Convert.ToInt32(await db.ValueAsync("SELECT COUNT(*) FROM pragma_table_info('episodes') WHERE name='hls_ref'")) > 0;
        if (!hasHls) await db.ExecAsync("ALTER TABLE episodes ADD COLUMN hls_ref TEXT NOT NULL DEFAULT ''");
        // The persisted prefix never changes when a story slug/title is edited.
        await db.ExecAsync("UPDATE stories SET storage_prefix = 'stories/' || slug || '--' || id WHERE storage_prefix = ''");
        // V1.9: seed the catalog; preserve legacy single-genre story assignments.
        var names = new[] {"Sắc Hiệp","Tiên Hiệp","Huyền Huyễn","Kiếm Hiệp","Xuyên Không",
            "Đô Thị","Đồng Nhân","Ngôn Tình","Lịch Sử","Khoa Huyễn","Trọng Sinh","Khác"};
        for (var i = 0; i < names.Length; i++)
            await db.ExecAsync("INSERT OR IGNORE INTO genres(slug,name,sort_order) VALUES(@slug,@name,@order)",
                ("@slug", Slugs.FromTitle(names[i])), ("@name", names[i]), ("@order", i));
        var legacy = await db.ListAsync("SELECT DISTINCT trim(genre) AS name FROM stories WHERE trim(genre)<>''");
        foreach (var row in legacy)
        {
            var name = row.S("name");
            await db.ExecAsync("INSERT OR IGNORE INTO genres(slug,name,sort_order) VALUES(@slug,@name,100)",
                ("@slug", Slugs.FromTitle(name)), ("@name", name));
        }
        await db.ExecAsync(@"INSERT OR IGNORE INTO story_genres(story_id,genre_slug)
            SELECT s.id,g.slug FROM stories s JOIN genres g ON g.name=s.genre COLLATE NOCASE
            WHERE trim(s.genre)<>''");
        var now = Helpers.Now();
        await db.ExecAsync("INSERT OR IGNORE INTO subscription_plans(id,code,name,price_vnd,duration_months,updated_at) VALUES('premium','PREMIUM','Premium 1 tháng',100000,1,@now)", ("@now", now));
        await db.ExecAsync("INSERT OR IGNORE INTO bank_accounts(id,updated_at) VALUES(1,@now)", ("@now", now));
        // One-time, idempotent migration from the former single receiving account.
        // Never restore a bank the admin later disabled or changed.
        var bankMigration = await db.ValueAsync("SELECT key FROM app_migrations WHERE key='receivers-v1.9.2'");
        if (bankMigration is null)
        {
            await using var tx = (SqliteTransaction)await db.BeginTransactionAsync();
            await using (var cmd = DbExtensions.Cmd(db, @"INSERT INTO bank_receivers(id,label,bank_bin,bank_name,account_number,account_name,is_active,is_default,created_at,updated_at)
                SELECT @id,'Tài khoản nhận tiền',bank_bin,bank_name,account_number,account_name,1,1,@now,@now
                FROM bank_accounts WHERE id=1 AND bank_bin<>'' AND account_number<>'' AND account_name<>''
                  AND NOT EXISTS(SELECT 1 FROM bank_receivers)", tx, ("@id",Helpers.Id()),("@now",now))) await cmd.ExecuteNonQueryAsync();
            await using (var cmd = DbExtensions.Cmd(db,"INSERT OR IGNORE INTO app_migrations(key,applied_at) VALUES('receivers-v1.9.2',@now)",tx,("@now",now))) await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }
        // The orders table stores immutable transfer details even when a bank is edited/deactivated.
        var hasReceiverId = Convert.ToInt32(await db.ValueAsync("SELECT COUNT(*) FROM pragma_table_info('orders') WHERE name='bank_receiver_id'")) > 0;
        if (!hasReceiverId) await db.ExecAsync("ALTER TABLE orders ADD COLUMN bank_receiver_id TEXT NOT NULL DEFAULT ''");
        // V1.9.3: distinguish purchases from administrator-granted extensions.
        // Existing orders are purchases. Do not overwrite existing purchase history.
        foreach (var (name, definition) in new[]
        {
            ("order_type", "TEXT NOT NULL DEFAULT 'Purchase' CHECK(order_type IN ('Purchase','AdminGrant'))"),
            ("grant_reason", "TEXT NOT NULL DEFAULT ''"),
            ("premium_start_at", "TEXT"),
            ("premium_end_at", "TEXT")
        })
        {
            var exists = Convert.ToInt32(await db.ValueAsync("SELECT COUNT(*) FROM pragma_table_info('orders') WHERE name=@name", ("@name", name))) > 0;
            if (!exists) await db.ExecAsync($"ALTER TABLE orders ADD COLUMN {name} {definition}");
        }
        // Backfill prior Admin grants (V1.9.2 and earlier) into the unified order history.
        // Only subscriptions without an order are touched; running startup twice is safe.
        var oldAdminGrants = await db.ListAsync(@"SELECT id,user_id,starts_at,ends_at,granted_by,note,created_at
            FROM subscriptions WHERE source='Admin' AND order_id IS NULL ORDER BY created_at,id");
        foreach (var prior in oldAdminGrants)
        {
            await using var tx = (SqliteTransaction)await db.BeginTransactionAsync();
            var orderId = Helpers.Id();
            var startAt = DateTimeOffset.Parse(prior.S("starts_at"), CultureInfo.InvariantCulture);
            var endAt = DateTimeOffset.Parse(prior.S("ends_at"), CultureInfo.InvariantCulture);
            var months = Math.Clamp((endAt.Year - startAt.Year) * 12 + endAt.Month - startAt.Month, 1, 36);
            var code = "LKHADOLD" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            await using (var cmd = DbExtensions.Cmd(db, @"INSERT INTO orders
                (id,code,user_id,plan_id,amount_vnd,duration_months,bank_receiver_id,bank_bin,bank_name,
                 account_number,account_name,status,created_at,expires_at,reviewed_by,review_note,
                 order_type,grant_reason,premium_start_at,premium_end_at)
                VALUES(@id,@code,@user,'premium',0,@months,'','','','','','Paid',@created,@created,@admin,@note,
                'AdminGrant',@note,@start,@end)", tx,
                ("@id",orderId),("@code",code),("@user",prior.S("user_id")),("@months",months),
                ("@created",prior.S("created_at")),("@admin",string.IsNullOrEmpty(prior.S("granted_by")) ? (object)DBNull.Value : prior.S("granted_by")),
                ("@note",prior.S("note")),("@start",prior.S("starts_at")),("@end",prior.S("ends_at"))))
                await cmd.ExecuteNonQueryAsync();
            await using (var cmd = DbExtensions.Cmd(db,
                "UPDATE subscriptions SET order_id=@order WHERE id=@id AND order_id IS NULL", tx,
                ("@order",orderId),("@id",prior.S("id"))))
                await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }
    }
}

public static class DbExtensions
{
    public static async Task<int> ExecAsync(this SqliteConnection db, string sql, params (string key, object? value)[] args)
    {
        await using var cmd = Cmd(db, sql, null, args);
        return await cmd.ExecuteNonQueryAsync();
    }
    public static async Task<object?> ValueAsync(this SqliteConnection db, string sql, params (string key, object? value)[] args)
    {
        await using var cmd = Cmd(db, sql, null, args);
        return await cmd.ExecuteScalarAsync();
    }
    public static async Task<List<Dictionary<string, object?>>> ListAsync(this SqliteConnection db, string sql, params (string key, object? value)[] args)
    {
        await using var cmd = Cmd(db, sql, null, args);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<Dictionary<string, object?>>();
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            list.Add(row);
        }
        return list;
    }
    public static SqliteCommand Cmd(SqliteConnection db, string sql, SqliteTransaction? tx = null, params (string key, object? value)[] args)
    {
        var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        if (tx is not null) cmd.Transaction = tx;
        foreach (var (key, value) in args) cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return cmd;
    }
    public static async Task<Dictionary<string, object?>?> FirstAsync(this SqliteConnection db, string sql, params (string key, object? value)[] args)
        => (await db.ListAsync(sql, args)).FirstOrDefault();
}

public static class Helpers
{
    public static string Now() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    public static string Id() => Guid.NewGuid().ToString("N");
    public static string S(this Dictionary<string, object?> row, string key) => Convert.ToString(row.GetValueOrDefault(key), CultureInfo.InvariantCulture) ?? "";
    public static int N(this Dictionary<string, object?> row, string key) => Convert.ToInt32(row.GetValueOrDefault(key) ?? 0, CultureInfo.InvariantCulture);
    public static object SafeUser(Dictionary<string, object?> u, string endsAt) => new
    {
        id = u.S("id"), username = u.S("username"), fullName = u.S("full_name"), phone = u.S("phone"),
        address = u.S("address"), role = u.S("role"), status = u.S("status"),
        premiumUntil = endsAt, isPremium = IsPremium(endsAt), createdAt = u.S("created_at")
    };
    public static bool IsPremium(string? endsAt) => DateTimeOffset.TryParse(endsAt, out var t) && t > DateTimeOffset.UtcNow;
    public static async Task<string> PremiumUntil(SqliteConnection db, string userId) =>
        Convert.ToString(await db.ValueAsync("SELECT MAX(ends_at) FROM subscriptions WHERE user_id=@id", ("@id", userId))) ?? "";
    public static string UserId(System.Security.Claims.ClaimsPrincipal p) => p.FindFirst("uid")?.Value ?? "";
    public static bool IsAdmin(System.Security.Claims.ClaimsPrincipal p) => p.IsInRole("Admin");
    public static async Task Audit(SqliteConnection db, string? actor, string action, string? subject, string details, SqliteTransaction? tx = null)
    {
        await using var cmd = DbExtensions.Cmd(db,
            "INSERT INTO audit_logs(id,actor_id,action,subject_id,details,created_at) VALUES(@id,@a,@ac,@s,@d,@t)",tx,
            ("@id",Id()),("@a",actor),("@ac",action),("@s",subject),("@d",details),("@t",Now()));
        await cmd.ExecuteNonQueryAsync();
    }
}

// Keep slug stable after creation, so URLs, bookmarks and R2 prefixes do not break on title edits.
public static class Slugs
{
    public static string FromTitle(string title)
    {
        var normalized = (title ?? "").ToLowerInvariant().Replace('đ','d').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch);
        }
        var slug = Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length > 130) slug = slug[..130].TrimEnd('-');
        return string.IsNullOrEmpty(slug) ? "truyen" : slug;
    }
}
