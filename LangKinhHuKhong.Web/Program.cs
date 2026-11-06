using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using LangKinhHuKhong;
using LangKinhHuKhong.Components;
using LangKinhHuKhong.Components.Shared;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.Data.Sqlite;
using Amazon.S3;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
// Keep SQLite and Data Protection keys outside Publish/IIS website folder.
// On IIS set LKH_DATA_ROOT to a persistent, ACL-protected local disk path.
var configuredDataRoot = Environment.GetEnvironmentVariable("LKH_DATA_ROOT");
var persistentDataRoot = Path.GetFullPath(
    string.IsNullOrWhiteSpace(configuredDataRoot)
        ? Path.Combine(builder.Environment.ContentRootPath, "data")
        : configuredDataRoot);
Directory.CreateDirectory(persistentDataRoot);
var keyRoot = Path.Combine(persistentDataRoot, "dataprotection-keys");
Directory.CreateDirectory(keyRoot);
builder.Services.AddDataProtection()
    .SetApplicationName("LangKinhHuKhong.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(keyRoot));
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 280L * 1024 * 1024);
// Public canonical base is fixed/configured, never taken from the incoming Host header.
var publicOrigin = (builder.Configuration["LKH_PUBLIC_ORIGIN"] ?? "https://langkinhhukhong.duckdns.org").TrimEnd('/');
if (!Uri.TryCreate(publicOrigin, UriKind.Absolute, out var publicUri) ||
    publicUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(publicUri.AbsolutePath.Trim('/')))
    throw new InvalidOperationException("LKH_PUBLIC_ORIGIN must be an HTTPS origin without a path.");
builder.Services.AddSingleton(new SeoSiteSettings(publicOrigin));
builder.Services.AddScoped<SeoService>();
builder.Services.AddScoped<BrowserApi>();
builder.Services.AddSingleton<Database>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("VietQrBanks", c => c.Timeout = TimeSpan.FromSeconds(8));
builder.Services.AddSingleton<AudioStorage>();
builder.Services.AddSingleton<StorageJanitor>();
builder.Services.AddSingleton<GoogleDriveService>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents(o =>
{
    o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);
});
builder.Services.AddSignalR(o =>
{
    o.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
    o.KeepAliveInterval = TimeSpan.FromSeconds(15);
    o.MaximumReceiveMessageSize = 10L * 1024 * 1024;
});
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 260L * 1024 * 1024);
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "lkh.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(o => o.AddPolicy("Admin", p => p.RequireRole("Admin")));
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "lkh.antiforgery"; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; });
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
    o.AddPolicy("write", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: (Helpers.UserId(ctx.User) is { Length: > 0 } uid ? "u:" + uid : "ip:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown")),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
    o.RejectionStatusCode = 429;
});
var app = builder.Build();
// SSL is terminated by Nginx on this same host (127.0.0.1 -> Kestrel).
// Keep default trusted loopback proxy; do not trust arbitrary Forwarded Headers.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
var database = app.Services.GetRequiredService<Database>();
// Validate Cloudflare R2 configuration on startup when R2 mode is enabled.
_ = app.Services.GetRequiredService<AudioStorage>();
await database.InitializeAsync();
await SeedAdminAsync(app.Services, database);

if (!app.Environment.IsDevelopment()) app.UseHsts();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; " +
        "worker-src 'self' blob:; media-src 'self' blob:; img-src 'self' data: https://img.vietqr.io https:; " +
        "style-src 'self' 'unsafe-inline'; connect-src 'self' ws: wss: https://cdn.jsdelivr.net; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});
app.UseRouting();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
// CSRF applies also to unauthenticated login/register. Token is obtained through GET /api/auth/csrf.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api") &&
        ctx.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
    {
        var path = ctx.Request.Path.Value ?? "";
        var isAdminUpload = path.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase) &&
            (path.EndsWith("/episodes", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith("/cover", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith("/audio", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith("/texts", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith("/texts/create", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("/admin/texts/", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("/admin/gdrive/", StringComparison.OrdinalIgnoreCase));
        if (!isAdminUpload && ctx.Request.ContentLength is > 256 * 1024)
        {
            ctx.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await ctx.Response.WriteAsJsonAsync(new { error = "Dữ liệu gửi lên vượt quá giới hạn cho phép." });
            return;
        }
        try { await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx); }
        catch (AntiforgeryValidationException) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { error = "CSRF không hợp lệ. Hãy tải lại trang." }); return; }
    }
    // Cookie role alone is not enough: always re-check user status, current role and password stamp.
    if (ctx.Request.Path.StartsWithSegments("/api") && ctx.User.Identity?.IsAuthenticated == true)
    {
        await using var db = await database.OpenAsync();
        var u = await db.FirstAsync("SELECT id,role,status,password_hash FROM users WHERE id=@id", ("@id", Helpers.UserId(ctx.User)));
        var pver = ctx.User.FindFirst("pver")?.Value;
        var invalidStamp = pver is not null && u is not null && pver != Helpers.PasswordStamp(u.S("password_hash"));
        if (u is null || u.S("status") != "Active" || u.S("role") != (Helpers.IsAdmin(ctx.User) ? "Admin" : "User") || invalidStamp)
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "Phiên đăng nhập không còn hiệu lực." });
            return;
        }
    }
    await next();
});

// Public SEO endpoints. Only Published stories enter the XML sitemap.
app.MapGet("/robots.txt", (SeoSiteSettings site) => Results.Text(
    "User-agent: *\nDisallow: /quan-tri\nDisallow: /tai-khoan\nDisallow: /lich-su\n" +
    "Disallow: /api/auth/\nDisallow: /api/admin/\nDisallow: /api/orders\n" +
    "Disallow: /api/episodes/\nSitemap: " + site.Absolute("/sitemap.xml") + "\n",
    "text/plain; charset=utf-8"));
app.MapGet("/sitemap.xml", async (Database source, SeoSiteSettings site) =>
    Results.Text(await SeoSitemap.Index(source, site), "application/xml; charset=utf-8"));
app.MapGet("/sitemap-static.xml", async (Database source, SeoSiteSettings site) =>
    Results.Text(await SeoSitemap.Static(source, site), "application/xml; charset=utf-8"));
app.MapGet("/sitemap-stories-{page:int}.xml", async Task<IResult> (int page, Database source, SeoSiteSettings site) =>
{
    var xml = await SeoSitemap.Stories(source, site, page);
    return xml is null ? Results.NotFound() : Results.Text(xml, "application/xml; charset=utf-8");
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", app = "Lăng Kính Hư Không", now = Helpers.Now() }));
app.MapGet("/api/auth/csrf", (HttpContext ctx, IAntiforgery anti) => Results.Ok(new { token = anti.GetAndStoreTokens(ctx).RequestToken }));
app.MapPost("/api/auth/register", async Task<IResult> (RegisterReq req, Database source, IPasswordHasher<AppUser> hasher) =>
{
    var username = req.Username.Trim().ToLowerInvariant();
    if (!Regex.IsMatch(username, "^[a-z0-9_]{3,30}$")) return Bad("Username 3–30 ký tự: a-z, 0-9, dấu gạch dưới.");
    if (req.Password.Length < 10 || req.Password.Length > 128) return Bad("Mật khẩu cần 10–128 ký tự.");
    if (req.FullName?.Length > 100 || req.Phone?.Length > 25 || req.Address?.Length > 300) return Bad("Thông tin hồ sơ quá dài.");
    var user = new AppUser { Id = Helpers.Id(), Username = username };
    var hash = hasher.HashPassword(user, req.Password);
    await using var db = await source.OpenAsync();
    try
    {
        await db.ExecAsync("INSERT INTO users(id,username,password_hash,full_name,phone,address,created_at,updated_at) VALUES(@id,@u,@pw,@name,@phone,@addr,@now,@now)",
            ("@id", user.Id), ("@u", username), ("@pw", hash), ("@name", (req.FullName ?? "").Trim()),
            ("@phone", (req.Phone ?? "").Trim()), ("@addr", (req.Address ?? "").Trim()), ("@now", Helpers.Now()));
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { return Results.Conflict(new { error = "Username đã tồn tại." }); }
    return Results.Ok(new { message = "Đăng ký thành công. Hãy đăng nhập." });
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/login", async Task<IResult> (LoginReq req, HttpContext ctx, Database source, IPasswordHasher<AppUser> hasher) =>
{
    const string dummyHash = "AQAAAAIAAYagAAAAEBWFTXPbqN9JBzQgvrEQMX+XprLPfLx9Pmt0jTsif3K907RzQaUjyIAcTPSK2mILqA==";
    await using var db = await source.OpenAsync();
    var u = await db.FirstAsync("SELECT * FROM users WHERE username=@u", ("@u", (req.Username ?? "").Trim().ToLowerInvariant()));
    if (u is null)
    {
        _ = hasher.VerifyHashedPassword(new AppUser { Id = "0", Username = "none" }, dummyHash, req.Password ?? "");
        return Results.Json(new { error = "Sai tài khoản hoặc mật khẩu." }, statusCode: 401);
    }
    var identity = new AppUser { Id = u.S("id"), Username = u.S("username") };
    var currentHash = u.S("password_hash");
    var result = hasher.VerifyHashedPassword(identity, currentHash, req.Password ?? "");
    if (result == PasswordVerificationResult.Failed) return Results.Json(new { error = "Sai tài khoản hoặc mật khẩu." }, statusCode: 401);
    if (u.S("status") != "Active") return Results.Json(new { error = "Tài khoản đã bị khóa hoặc ngừng hoạt động." }, statusCode: 403);
    if (result == PasswordVerificationResult.SuccessRehashNeeded)
    {
        currentHash = hasher.HashPassword(identity, req.Password!);
        await db.ExecAsync("UPDATE users SET password_hash=@hash,updated_at=@at WHERE id=@id", ("@hash", currentHash), ("@at", Helpers.Now()), ("@id", identity.Id));
    }
    var claims = new[]
    {
        new Claim("uid", identity.Id),
        new Claim(ClaimTypes.Name, identity.Username),
        new Claim(ClaimTypes.Role, u.S("role")),
        new Claim("pver", Helpers.PasswordStamp(currentHash))
    };
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.Ok(new { user = Helpers.SafeUser(u, await Helpers.PremiumUntil(db, identity.Id)) });
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/logout", async Task<IResult> (HttpContext ctx) => { await ctx.SignOutAsync(); return Results.Ok(new { message = "Đã đăng xuất" }); }).RequireAuthorization();
app.MapGet("/api/auth/me", async Task<IResult> (HttpContext ctx, Database source) =>
{
    await using var db = await source.OpenAsync();
    var u = await db.FirstAsync("SELECT * FROM users WHERE id=@id", ("@id", Helpers.UserId(ctx.User)));
    if (u is null) return Results.Unauthorized();
    return Results.Ok(Helpers.SafeUser(u, await Helpers.PremiumUntil(db, u.S("id"))));
}).RequireAuthorization();
app.MapPut("/api/auth/profile", async Task<IResult> (ProfileReq req, HttpContext ctx, Database source) =>
{
    if (req.FullName.Length > 100 || req.Phone.Length > 25 || req.Address.Length > 300) return Bad("Thông tin hồ sơ quá dài.");
    await using var db = await source.OpenAsync();
    await db.ExecAsync("UPDATE users SET full_name=@name,phone=@phone,address=@addr,updated_at=@now WHERE id=@id",
        ("@name",req.FullName.Trim()),("@phone",req.Phone.Trim()),("@addr",req.Address.Trim()),("@now",Helpers.Now()),("@id",Helpers.UserId(ctx.User)));
    return Results.Ok(new { message = "Đã cập nhật hồ sơ." });
}).RequireAuthorization().RequireRateLimiting("write");

// Enables rotation of the bundled Development admin password before deployment.
app.MapPost("/api/auth/change-password", async Task<IResult> (ChangePasswordReq req, HttpContext ctx, Database source, IPasswordHasher<AppUser> hasher) =>
{
    if (string.IsNullOrWhiteSpace(req.CurrentPassword) || req.NewPassword is null ||
        req.NewPassword.Length < 12 || req.NewPassword.Length > 128)
        return Bad("Mật khẩu mới phải có từ 12 đến 128 ký tự.");
    if (req.CurrentPassword == req.NewPassword)
        return Bad("Mật khẩu mới phải khác mật khẩu đang dùng.");
    await using var db = await source.OpenAsync();
    var row = await db.FirstAsync("SELECT id,username,password_hash FROM users WHERE id=@id", ("@id", Helpers.UserId(ctx.User)));
    if (row is null) return Results.Unauthorized();
    var identity = new AppUser { Id = row.S("id"), Username = row.S("username") };
    if (hasher.VerifyHashedPassword(identity, row.S("password_hash"), req.CurrentPassword) == PasswordVerificationResult.Failed)
        return Bad("Mật khẩu hiện tại không chính xác.");
    var newHash = hasher.HashPassword(identity, req.NewPassword);
    await db.ExecAsync("UPDATE users SET password_hash=@hash,updated_at=@at WHERE id=@id",
        ("@hash", newHash), ("@at", Helpers.Now()), ("@id", identity.Id));
    await Helpers.Audit(db, identity.Id, "user.password_change", identity.Id, "Password rotated");
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { message = "Đã đổi mật khẩu. Vui lòng đăng nhập lại." });
}).RequireAuthorization().RequireRateLimiting("auth");

// Public library metadata. Full audio itself is NEVER exposed here.
// V1.9: public catalog, multi-genre filtering and unique qualified audience ranking.
app.MapGet("/api/genres", async Task<IResult> (Database source) =>
{
    await using var db = await source.OpenAsync();
    return Results.Ok(await db.ListAsync("SELECT slug,name FROM genres WHERE is_active=1 ORDER BY sort_order,name"));
});
app.MapGet("/api/stories", async Task<IResult> (Database source, string? q, string? genre, string? sort) =>
{
    var order = sort switch
    {
        "popular" => "listenerCount DESC,s.created_at DESC",
        "new" => "s.created_at DESC",
        _ => "s.updated_at DESC"
    };
    await using var db = await source.OpenAsync();
    var rows = await db.ListAsync(@"SELECT s.id,s.slug,s.title,s.author,s.genre,s.description,s.created_at,
        CASE WHEN s.cover_file<>'' THEN '/api/stories/' || s.id || '/cover' ELSE s.cover_url END AS cover_url,s.status,
        COALESCE((SELECT group_concat(g.name,' | ') FROM story_genres sg JOIN genres g ON g.slug=sg.genre_slug
            WHERE sg.story_id=s.id),'') AS genres,
        (SELECT COUNT(*) FROM episodes e WHERE e.story_id=s.id AND e.status='Published') AS episodeCount,
        (SELECT COUNT(*) FROM story_listeners l WHERE l.story_id=s.id AND l.qualified_at IS NOT NULL) AS listenerCount,
        (SELECT COUNT(*) FROM story_reviews r WHERE r.story_id=s.id AND r.status='Visible') AS ratingCount,
        COALESCE((SELECT ROUND(AVG(r.stars),1) FROM story_reviews r WHERE r.story_id=s.id AND r.status='Visible'),0) AS ratingAverage
        FROM stories s WHERE s.status='Published' AND (@q='' OR s.title LIKE @term OR s.author LIKE @term)
        AND (@genre='' OR EXISTS(SELECT 1 FROM story_genres sg WHERE sg.story_id=s.id AND sg.genre_slug=@genre))
        ORDER BY " + order + " LIMIT 200",
        ("@q",q ?? ""),("@term","%"+(q ?? "")+"%"),("@genre",genre ?? ""));
    return Results.Ok(rows);
});
app.MapGet("/api/stories/{slug}", async Task<IResult> (string slug, Database source) =>
{
    await using var db = await source.OpenAsync();
    var story = await db.FirstAsync(@"SELECT id,slug,title,author,genre,description,created_at,
        CASE WHEN cover_file<>'' THEN '/api/stories/' || id || '/cover' ELSE cover_url END AS cover_url,status,
        COALESCE((SELECT group_concat(g.name,' | ') FROM story_genres sg JOIN genres g ON g.slug=sg.genre_slug WHERE sg.story_id=stories.id),'') AS genres,
        (SELECT COUNT(*) FROM story_listeners l WHERE l.story_id=stories.id AND l.qualified_at IS NOT NULL) AS listenerCount,
        (SELECT COUNT(*) FROM story_reviews r WHERE r.story_id=stories.id AND r.status='Visible') AS ratingCount,
        COALESCE((SELECT ROUND(AVG(r.stars),1) FROM story_reviews r WHERE r.story_id=stories.id AND r.status='Visible'),0) AS ratingAverage
        FROM stories WHERE slug=@slug AND status='Published'",("@slug",slug));
    if (story is null) return Results.NotFound();
    var eps = await db.ListAsync("SELECT id,title,episode_number,duration_seconds,created_at FROM episodes WHERE story_id=@id AND status='Published' ORDER BY episode_number",("@id",story.S("id")));
    return Results.Ok(new { story, episodes = eps });
});
app.MapGet("/api/stories/{slug}/reviews", async Task<IResult> (string slug, HttpContext ctx, Database source) =>
{
    await using var db=await source.OpenAsync();
    var story=await db.FirstAsync("SELECT id FROM stories WHERE slug=@s AND status='Published'",("@s",slug));
    if(story is null) return Results.NotFound();
    return Results.Ok(await db.ListAsync(@"SELECT r.id,r.stars,r.body,r.status,r.updated_at,u.username,
      CASE WHEN r.user_id=@user THEN 1 ELSE 0 END AS is_mine
      FROM story_reviews r JOIN users u ON u.id=r.user_id
      WHERE r.story_id=@story AND (r.status='Visible' OR (@user<>'' AND r.user_id=@user)) AND u.status='Active'
      ORDER BY r.updated_at DESC LIMIT 100",("@user",Helpers.UserId(ctx.User)),("@story",story.S("id"))));
});
app.MapPut("/api/stories/{slug}/reviews/me", async Task<IResult> (string slug, ReviewReq req, HttpContext ctx, Database source) =>
{
    if(req.Stars is < 1 or > 5 || req.Body is null || req.Body.Trim().Length > 1200)
        return Bad("Đánh giá cần 1–5 sao và bình luận không quá 1.200 ký tự.");
    await using var db=await source.OpenAsync();
    var story=await db.FirstAsync("SELECT id FROM stories WHERE slug=@s AND status='Published'",("@s",slug));
    if(story is null) return Results.NotFound();
    var uid=Helpers.UserId(ctx.User); var now=Helpers.Now();
    await db.ExecAsync(@"INSERT INTO story_reviews(id,story_id,user_id,stars,body,created_at,updated_at)
      VALUES(@id,@story,@user,@stars,@body,@now,@now)
      ON CONFLICT(story_id,user_id) DO UPDATE SET stars=excluded.stars,body=excluded.body,updated_at=excluded.updated_at",
      ("@id",Helpers.Id()),("@story",story.S("id")),("@user",uid),("@stars",req.Stars),("@body",req.Body.Trim()),("@now",now));
    var status = Convert.ToString(await db.ValueAsync("SELECT status FROM story_reviews WHERE story_id=@story AND user_id=@user",("@story",story.S("id")),("@user",uid))) ?? "Visible";
    var message = status == "Hidden"
        ? "Đã cập nhật nội dung, nhưng đánh giá của bạn hiện đang bị Quản trị viên tạm ẩn nên chưa hiển thị công khai."
        : "Đã lưu đánh giá. Mỗi tài khoản có một đánh giá cho mỗi truyện.";
    return Results.Ok(new { message, status });
}).RequireAuthorization().RequireRateLimiting("write");
app.MapDelete("/api/stories/{slug}/reviews/me", async Task<IResult> (string slug, HttpContext ctx, Database source) =>
{
    await using var db=await source.OpenAsync();
    await db.ExecAsync("DELETE FROM story_reviews WHERE user_id=@u AND story_id IN (SELECT id FROM stories WHERE slug=@s)",
      ("@u",Helpers.UserId(ctx.User)),("@s",slug));
    return Results.Ok(new {message="Đã xóa đánh giá của bạn."});
}).RequireAuthorization().RequireRateLimiting("write");
// Published covers are public via the application. Draft/hidden covers are Admin-only.
// All image objects remain private inside R2; no storage keys are returned to users.
app.MapMethods("/api/stories/{id}/cover", ["GET", "HEAD"], async Task (string id, HttpContext ctx, Database source, AudioStorage storage) =>
{
    await using var db = await source.OpenAsync();
    var story = await db.FirstAsync("SELECT cover_file,status FROM stories WHERE id=@id",("@id",id));
    if (story is null || string.IsNullOrWhiteSpace(story.S("cover_file")) ||
        (story.S("status") != "Published" && !Helpers.IsAdmin(ctx.User)))
    { ctx.Response.StatusCode = 404; return; }
    await storage.StreamCoverAsync(ctx, story.S("cover_file"));
});
// V1.8: plain MP3 is preview ONLY. Full MP3 cannot be downloaded through this route,
// even with a Premium session. Free streaming deliberately uses no HLS/DRM.
app.MapMethods("/api/episodes/{episodeId}/stream", ["GET", "HEAD"], async Task (string episodeId, HttpContext ctx, Database source, AudioStorage storage) =>
{
    await using var db = await source.OpenAsync();
    var ep = await db.FirstAsync(@"SELECT e.preview_file,e.status,s.status AS story_status
         FROM episodes e JOIN stories s ON s.id=e.story_id WHERE e.id=@id",("@id",episodeId));
    if (ep is null || ep.S("status") != "Published" || ep.S("story_status") != "Published")
    { ctx.Response.StatusCode = 404; return; }
    await storage.StreamAsync(ctx, ep.S("preview_file"));
}).RequireAuthorization();

// Each playlist, segment and AES-128 key is guarded by a fresh database entitlement check.
// A Premium license expiry or locked account immediately stops NEW segment/key requests.
app.MapGet("/api/episodes/{episodeId}/hls/{filename}", async Task (string episodeId, string filename,
    HttpContext ctx, Database source, AudioStorage storage) =>
{
    ctx.Response.Headers.CacheControl="private, no-store";
    if (!Helpers.IsAdmin(ctx.User))
    {
        await using var ent = await source.OpenAsync();
        if (!Helpers.IsPremium(await Helpers.PremiumUntil(ent, Helpers.UserId(ctx.User))))
        { ctx.Response.StatusCode=403; return; }
    }
    await using var db=await source.OpenAsync();
    var ep=await db.FirstAsync(@"SELECT e.hls_ref,e.status,s.status AS story_status
        FROM episodes e JOIN stories s ON e.story_id=s.id WHERE e.id=@id",("@id",episodeId));
    if(ep is null || ep.S("status")!="Published" || ep.S("story_status")!="Published")
    { ctx.Response.StatusCode=404; return; }
    if (ep.S("hls_ref")=="") { ctx.Response.StatusCode=409; return; }
    await storage.StreamHlsPartAsync(ctx,ep.S("hls_ref"),filename);
}).RequireAuthorization();
app.MapPut("/api/episodes/{episodeId}/progress", async Task<IResult> (string episodeId, ProgressReq req, HttpContext ctx, Database source) =>
{
    await using var db = await source.OpenAsync();
    var ep = await db.FirstAsync("SELECT duration_seconds FROM episodes WHERE id=@id",("@id",episodeId));
    if (ep is null) return Results.NotFound();
    var premium = Helpers.IsAdmin(ctx.User) || Helpers.IsPremium(await Helpers.PremiumUntil(db, Helpers.UserId(ctx.User)));
    var maxSeconds = premium ? ep.N("duration_seconds") : Math.Min(300, ep.N("duration_seconds"));
    var seconds = Math.Clamp(req.Seconds, 0, Math.Max(maxSeconds, 0));
    await db.ExecAsync(@"INSERT INTO listening_progress(user_id,episode_id,seconds,updated_at) VALUES(@u,@e,@s,@at)
       ON CONFLICT(user_id,episode_id) DO UPDATE SET seconds=excluded.seconds,updated_at=excluded.updated_at",
       ("@u",Helpers.UserId(ctx.User)),("@e",episodeId),("@s",seconds),("@at",Helpers.Now()));
    return Results.Ok(new { seconds });
}).RequireAuthorization();
// Listening counts are NOT based on currentTime or seeks. Only real elapsed server time
// between heartbeats is credited; a user may qualify once per story after >300 seconds.
app.MapPost("/api/episodes/{episodeId}/playback/start", async Task<IResult> (string episodeId, HttpContext ctx, Database source) =>
{
    if(Helpers.IsAdmin(ctx.User)) return Results.Ok(new { sessionId="" });
    await using var db=await source.OpenAsync();
    var ep=await db.FirstAsync(@"SELECT e.id FROM episodes e JOIN stories s ON s.id=e.story_id
      WHERE e.id=@e AND e.status='Published' AND s.status='Published'",("@e",episodeId));
    if(ep is null) return Results.NotFound();
    var sessionId=Helpers.Id();
    await db.ExecAsync(@"INSERT INTO playback_sessions(user_id,session_id,episode_id,last_tick)
      VALUES(@user,@session,@episode,@tick)
      ON CONFLICT(user_id) DO UPDATE SET session_id=excluded.session_id,
        episode_id=excluded.episode_id,last_tick=excluded.last_tick",
        ("@user",Helpers.UserId(ctx.User)),("@session",sessionId),("@episode",episodeId),
        ("@tick",DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    return Results.Ok(new { sessionId });
}).RequireAuthorization();
app.MapPost("/api/episodes/{episodeId}/playback/ping", async Task<IResult> (string episodeId, HeartbeatReq req, HttpContext ctx, Database source) =>
{
    if(string.IsNullOrWhiteSpace(req.SessionId)) return Bad("Phiên nghe không hợp lệ.");
    await using var db=await source.OpenAsync();
    await using var tx=(SqliteTransaction)await db.BeginTransactionAsync();
    var userId=Helpers.UserId(ctx.User);
    var session=await RowTx(db,tx,@"SELECT p.last_tick,e.story_id FROM playback_sessions p
      JOIN episodes e ON e.id=p.episode_id JOIN stories s ON s.id=e.story_id
      WHERE p.user_id=@user AND p.session_id=@session AND p.episode_id=@ep
        AND e.status='Published' AND s.status='Published'",
      ("@user",userId),("@session",req.SessionId),("@ep",episodeId));
    if(session is null) return Results.Conflict(new {error="Phiên nghe đã kết thúc hoặc chuyển tập."});
    var nowTick=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var lastTick=Convert.ToInt64(session["last_tick"]);
    var delta=nowTick-lastTick;
    // Lost background tabs/paused requests cannot accumulate unattended playback.
    var credit=delta is >= 3 and <= 25 ? (int)delta : 0;
    await ExecTx(db,tx,"UPDATE playback_sessions SET last_tick=@tick WHERE user_id=@u AND session_id=@sid",
      ("@tick",nowTick),("@u",userId),("@sid",req.SessionId));
    if(credit>0)
    {
        await ExecTx(db,tx,@"INSERT INTO story_listeners(user_id,story_id,listened_seconds,qualified_at,updated_at)
          VALUES(@u,@s,@d,NULL,@at) ON CONFLICT(user_id,story_id) DO UPDATE SET
          listened_seconds=story_listeners.listened_seconds+excluded.listened_seconds,
          qualified_at=CASE WHEN story_listeners.qualified_at IS NULL
            AND story_listeners.listened_seconds+excluded.listened_seconds>300
            THEN excluded.updated_at ELSE story_listeners.qualified_at END,
          updated_at=excluded.updated_at",
          ("@u",userId),("@s",Convert.ToString(session["story_id"])!),("@d",credit),("@at",Helpers.Now()));
    }
    await tx.CommitAsync();
    return Results.Ok(new { creditedSeconds=credit });
}).RequireAuthorization();
app.MapGet("/api/listening", async Task<IResult> (HttpContext ctx, Database source) =>
{
    await using var db = await source.OpenAsync();
    return Results.Ok(await db.ListAsync(@"SELECT p.episode_id,p.seconds,p.updated_at,e.title AS episode_title,s.title AS story_title,s.slug,
        CASE WHEN s.cover_file<>'' THEN '/api/stories/' || s.id || '/cover' ELSE s.cover_url END AS cover_url
        FROM listening_progress p JOIN episodes e ON e.id=p.episode_id JOIN stories s ON s.id=e.story_id
        WHERE p.user_id=@u ORDER BY p.updated_at DESC LIMIT 30",("@u",Helpers.UserId(ctx.User))));
}).RequireAuthorization();

// Pricing and orders: bank information is snapshotted on each order.
app.MapGet("/api/plans", async Task<IResult> (Database source) =>
{
    await using var db = await source.OpenAsync();
    return Results.Ok(await db.ListAsync("SELECT id,code,name,price_vnd,duration_months FROM subscription_plans"));
});
app.MapPost("/api/orders", async Task<IResult> (HttpContext ctx, Database source) =>
{
    await using var db = await source.OpenAsync();
    await ExpireOrders(db);
    var userId = Helpers.UserId(ctx.User);
    var openOrders = Convert.ToInt32(await db.ValueAsync("SELECT COUNT(*) FROM orders WHERE user_id=@u AND order_type='Purchase' AND status IN ('Pending','Reviewing')", ("@u", userId)));
    if (openOrders >= 3)
        return Bad("Bạn đang có tối đa 3 đơn chờ xử lý. Hãy hoàn tất thanh toán hoặc đợi đơn cũ hết hạn.");
    var plan = await db.FirstAsync("SELECT * FROM subscription_plans WHERE id='premium'");
    var bank = await db.FirstAsync("SELECT * FROM bank_receivers WHERE is_active=1 AND is_default=1");
    if (plan is null || bank is null || string.IsNullOrWhiteSpace(bank.S("bank_bin")) || string.IsNullOrWhiteSpace(bank.S("account_number")) || string.IsNullOrWhiteSpace(bank.S("account_name")))
        return Results.BadRequest(new { error = "Admin chưa thiết lập tài khoản nhận thanh toán." });
    var code = "LKH" + DateTimeOffset.UtcNow.ToString("yyMMddHHmmss") + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    var id = Helpers.Id();
    await db.ExecAsync(@"INSERT INTO orders(id,code,user_id,plan_id,amount_vnd,duration_months,bank_receiver_id,bank_bin,bank_name,account_number,account_name,status,created_at,expires_at)
        VALUES(@id,@code,@user,@plan,@amount,@months,@bankId,@bin,@bank,@account,@name,'Pending',@now,@expire)",
        ("@id",id),("@code",code),("@user",userId),("@plan",plan.S("id")),("@amount",plan.N("price_vnd")),
        ("@months",plan.N("duration_months")),("@bankId",bank.S("id")),("@bin",bank.S("bank_bin")),("@bank",bank.S("bank_name")),
        ("@account",bank.S("account_number")),("@name",bank.S("account_name")),("@now",Helpers.Now()),
        ("@expire",DateTimeOffset.UtcNow.AddHours(24).ToString("O")));
    return Results.Ok(await OrderDto(db,id));
}).RequireAuthorization().RequireRateLimiting("write");
app.MapGet("/api/orders", async Task<IResult> (HttpContext ctx, Database source) =>
{
    await using var db = await source.OpenAsync();
    await ExpireOrders(db);
    var rows = await db.ListAsync(@"SELECT o.id,o.code,o.amount_vnd,o.duration_months,o.status,o.order_type,o.grant_reason,
        o.created_at,o.expires_at,o.paid_at,
        COALESCE(o.premium_start_at,(SELECT starts_at FROM subscriptions WHERE order_id=o.id LIMIT 1)) AS premium_start_at,
        COALESCE(o.premium_end_at,(SELECT ends_at FROM subscriptions WHERE order_id=o.id LIMIT 1)) AS premium_end_at,
        COALESCE(admin.username,'') AS granted_by_name
        FROM orders o LEFT JOIN users admin ON admin.id=o.reviewed_by
        WHERE o.user_id=@u ORDER BY o.created_at DESC LIMIT 100",("@u",Helpers.UserId(ctx.User)));
    return Results.Ok(rows);
}).RequireAuthorization();
app.MapGet("/api/orders/{id}", async Task<IResult> (string id, HttpContext ctx, Database source) =>
{
    await using var db = await source.OpenAsync();
    await ExpireOrders(db);
    var order = await db.FirstAsync("SELECT user_id FROM orders WHERE id=@id",("@id",id));
    if (order is null || (!Helpers.IsAdmin(ctx.User) && order.S("user_id") != Helpers.UserId(ctx.User))) return Results.NotFound();
    return Results.Ok(await OrderDto(db,id));
}).RequireAuthorization();
app.MapPost("/api/orders/{id}/reported", async Task<IResult> (string id, HttpContext ctx, Database source) =>
{
    await using var db = await source.OpenAsync();
    await ExpireOrders(db);
    var n = await db.ExecAsync("UPDATE orders SET status='Reviewing' WHERE id=@id AND user_id=@u AND order_type='Purchase' AND status='Pending'",("@id",id),("@u",Helpers.UserId(ctx.User)));
    return n == 1 ? Results.Ok(new { message = "Đã gửi thông báo, chờ Admin đối chiếu ngân hàng." }) : Bad("Đơn không hợp lệ hoặc đã xử lý.");
}).RequireAuthorization().RequireRateLimiting("write");

// Admin endpoints
var admin = app.MapGroup("/api/admin").RequireAuthorization("Admin");
admin.MapGet("/stats", async Task<IResult> (Database source) =>
{
    await using var db = await source.OpenAsync();
    var users = await db.ValueAsync("SELECT COUNT(*) FROM users WHERE status='Active'");
    var stories = await db.ValueAsync("SELECT COUNT(*) FROM stories");
    var episodes = await db.ValueAsync("SELECT COUNT(*) FROM episodes");
    var pending = await db.ValueAsync("SELECT COUNT(*) FROM orders WHERE status IN ('Pending','Reviewing')");
    var income = await db.ValueAsync("SELECT COALESCE(SUM(amount_vnd),0) FROM payments");
    var premium = await db.ValueAsync("SELECT COUNT(DISTINCT user_id) FROM subscriptions WHERE ends_at>@now",("@now",Helpers.Now()));
    return Results.Ok(new { users, stories, episodes, pending, income, premium });
});
admin.MapGet("/users", async Task<IResult> (Database source, string? q) =>
{
    await using var db = await source.OpenAsync();
    return Results.Ok(await db.ListAsync(@"SELECT u.id,u.username,u.full_name,u.phone,u.address,u.role,u.status,u.created_at,
        (SELECT MAX(ends_at) FROM subscriptions s WHERE s.user_id=u.id) AS premiumUntil
        FROM users u WHERE (@q='' OR u.username LIKE @term OR u.full_name LIKE @term OR u.phone LIKE @term)
        ORDER BY u.created_at DESC LIMIT 200",("@q",q??""),("@term","%"+(q??"")+"%")));
});
admin.MapPut("/users/{id}", async Task<IResult> (string id, AdminUserReq req, HttpContext ctx, Database source) =>
{
    if (req.FullName.Length > 100 || req.Phone.Length > 25 || req.Address.Length > 300 ||
        !new[] { "Active", "Locked", "Deleted" }.Contains(req.Status)) return Bad("Dữ liệu không hợp lệ.");
    if (id == Helpers.UserId(ctx.User) && req.Status != "Active") return Bad("Không thể tự khóa hoặc xóa tài khoản Admin đang đăng nhập.");
    await using var db = await source.OpenAsync();
    var u = await db.FirstAsync("SELECT role FROM users WHERE id=@id",("@id",id));
    if (u is null) return Results.NotFound();
    if (u.S("role") == "Admin" && id != Helpers.UserId(ctx.User)) return Bad("Không chỉnh sửa Admin khác trong phiên bản này.");
    await db.ExecAsync("UPDATE users SET full_name=@n,phone=@p,address=@a,status=@s,updated_at=@now WHERE id=@id",
        ("@n",req.FullName.Trim()),("@p",req.Phone.Trim()),("@a",req.Address.Trim()),("@s",req.Status),("@now",Helpers.Now()),("@id",id));
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"user.update",id,$"status={req.Status}");
    return Results.Ok(new { message = "Đã cập nhật tài khoản." });
});
admin.MapPost("/users/{id}/premium", async Task<IResult> (string id, GrantReq req, HttpContext ctx, Database source) =>
{
    if (req.Months < 1 || req.Months > 36 || string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length > 300) return Bad("Số tháng 1–36; cần nhập lý do cấp gói.");
    await using var db = await source.OpenAsync();
    if (await db.ValueAsync("SELECT id FROM users WHERE id=@id AND status='Active'",("@id",id)) is null) return Results.NotFound();
    await using var tx = (SqliteTransaction)await db.BeginTransactionAsync();
    var until = await PremiumUntilTx(db,tx,id);
    var alreadyPremium = Helpers.IsPremium(until);
    var start = alreadyPremium ? DateTimeOffset.Parse(until) : DateTimeOffset.UtcNow;
    var end = start.AddMonths(req.Months);
    var now = Helpers.Now();
    var adminId = Helpers.UserId(ctx.User);
    var reason = req.Reason.Trim();
    var orderId = Helpers.Id();
    var orderCode = "LKHAD" + DateTimeOffset.UtcNow.ToString("yyMMddHHmmss") + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    // A zero-value administrative record in Orders is NOT a bank payment.
    // Legacy Orders require bank fields and expiry, so leave bank fields empty.
    await ExecTx(db,tx,@"INSERT INTO orders
      (id,code,user_id,plan_id,amount_vnd,duration_months,bank_receiver_id,bank_bin,bank_name,
       account_number,account_name,status,created_at,expires_at,reviewed_by,review_note,
       order_type,grant_reason,premium_start_at,premium_end_at)
      VALUES(@id,@code,@user,'premium',0,@months,'','','','','','Paid',@now,@now,@admin,@reason,
       'AdminGrant',@reason,@start,@end)",
       ("@id",orderId),("@code",orderCode),("@user",id),("@months",req.Months),
       ("@now",now),("@admin",adminId),("@reason",reason),
       ("@start",start.ToString("O")),("@end",end.ToString("O")));
    await ExecTx(db,tx,@"INSERT INTO subscriptions(id,user_id,starts_at,ends_at,source,order_id,granted_by,note,created_at)
       VALUES(@id,@user,@start,@end,'Admin',@order,@admin,@note,@now)",
       ("@id",Helpers.Id()),("@user",id),("@start",start.ToString("O")),("@end",end.ToString("O")),
       ("@order",orderId),("@admin",adminId),("@note",reason),("@now",now));
    await Helpers.Audit(db,adminId,"premium.grant",id,$"order={orderCode}; months={req.Months}; reason={reason}",tx);
    await tx.CommitAsync();
    return Results.Ok(new { premiumUntil = end.ToString("O"), orderId, orderCode, source = "AdminGrant", isExtension = alreadyPremium });
});
admin.MapGet("/stories", async Task<IResult> (Database source) =>
{
    await using var db = await source.OpenAsync();
    return Results.Ok(await db.ListAsync(@"SELECT *,
       CASE WHEN cover_file<>'' THEN '/api/stories/' || id || '/cover' ELSE cover_url END AS cover_image_url
       ,COALESCE((SELECT group_concat(g.slug,'|') FROM story_genres sg JOIN genres g ON g.slug=sg.genre_slug
           WHERE sg.story_id=stories.id),'') AS genre_keys
       ,(SELECT COUNT(*) FROM episodes ep WHERE ep.story_id=stories.id) AS episode_count
       ,(SELECT COUNT(*) FROM story_text_files tf WHERE tf.story_id=stories.id) AS text_count
       FROM stories ORDER BY updated_at DESC LIMIT 300"));
});
// Admin: genre catalog. Disabling keeps existing story/category relations intact.
admin.MapGet("/genres", async Task<IResult> (Database source) =>
{
    await using var db=await source.OpenAsync();
    return Results.Ok(await db.ListAsync("SELECT slug,name,is_active,sort_order FROM genres ORDER BY sort_order,name"));
});
admin.MapPost("/genres", async Task<IResult> (GenreReq req, HttpContext ctx, Database source) =>
{
    if(req.Name is null || req.Name.Trim().Length is < 2 or > 60) return Bad("Tên thể loại phải từ 2–60 ký tự.");
    await using var db=await source.OpenAsync();
    var slug=await UniqueSlugAsync(db,req.Name,"genres");
    try { await db.ExecAsync("INSERT INTO genres(slug,name,is_active,sort_order) VALUES(@slug,@name,1,100)",
        ("@slug",slug),("@name",req.Name.Trim())); }
    catch(SqliteException ex) when(ex.SqliteErrorCode==19){return Results.Conflict(new{error="Tên thể loại đã tồn tại."});}
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"genre.create",slug,req.Name.Trim());
    return Results.Ok(new { slug });
});
admin.MapPut("/genres/{slug}", async Task<IResult> (string slug, GenreReq req, HttpContext ctx, Database source) =>
{
    if(req.Name is null || req.Name.Trim().Length is < 2 or > 60) return Bad("Tên thể loại phải từ 2–60 ký tự.");
    await using var db=await source.OpenAsync();
    try
    {
        var n=await db.ExecAsync("UPDATE genres SET name=@name,is_active=@active WHERE slug=@slug",
           ("@name",req.Name.Trim()),("@active",req.IsActive ? 1 : 0),("@slug",slug));
        if(n==0) return Results.NotFound();
    }
    catch(SqliteException ex) when(ex.SqliteErrorCode==19){return Results.Conflict(new{error="Tên thể loại đã tồn tại."});}
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"genre.update",slug,req.Name.Trim());
    return Results.Ok(new {message="Đã cập nhật thể loại."});
});
admin.MapDelete("/genres/{slug}", async Task<IResult> (string slug, HttpContext ctx, Database source) =>
{
    await using var db=await source.OpenAsync();
    var used=await db.ValueAsync("SELECT 1 FROM story_genres WHERE genre_slug=@slug LIMIT 1",("@slug",slug));
    if(used is not null) return Results.Conflict(new {error="Thể loại đang được gắn vào truyện. Hãy ẩn thay vì xóa."});
    await db.ExecAsync("DELETE FROM genres WHERE slug=@slug",("@slug",slug));
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"genre.delete",slug,"Deleted unused category");
    return Results.Ok(new {message="Đã xóa thể loại chưa sử dụng."});
});
admin.MapGet("/reviews", async Task<IResult> (Database source) =>
{
    await using var db=await source.OpenAsync();
    return Results.Ok(await db.ListAsync(@"SELECT r.id,r.stars,r.body,r.status,r.updated_at,
      u.username,s.title AS story_title FROM story_reviews r
      JOIN users u ON u.id=r.user_id JOIN stories s ON s.id=r.story_id
      ORDER BY r.updated_at DESC LIMIT 200"));
});
admin.MapPut("/reviews/{id}/visibility", async Task<IResult> (string id, ReviewVisibilityReq req, HttpContext ctx, Database source) =>
{
    if(req.Status is not ("Visible" or "Hidden")) return Bad("Trạng thái đánh giá không hợp lệ.");
    await using var db=await source.OpenAsync();
    if(await db.ExecAsync("UPDATE story_reviews SET status=@status WHERE id=@id",("@status",req.Status),("@id",id))==0)
        return Results.NotFound();
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"review.moderate",id,req.Status);
    return Results.Ok(new {message="Đã cập nhật hiển thị đánh giá."});
});
admin.MapPost("/stories", async Task<IResult> (StoryReq req, HttpContext ctx, Database source) =>
{
    var err=ValidateStory(req);
    if(err is not null) return Bad(err);
    var id=Helpers.Id();
    await using var db=await source.OpenAsync();
    var selected=await ResolveGenresAsync(db,req);
    if(selected is null) return Bad("Chọn ít nhất một thể loại đang hoạt động.");
    var slug=await UniqueSlugAsync(db,req.Title,"stories");
    var now=Helpers.Now();
    await using var tx=(SqliteTransaction)await db.BeginTransactionAsync();
    try
    {
        await ExecTx(db,tx,@"INSERT INTO stories(id,slug,title,author,genre,description,cover_url,storage_prefix,status,created_at,updated_at)
            VALUES(@id,@slug,@title,@author,@genre,@desc,@cover,@prefix,@status,@now,@now)",
            ("@id",id),("@slug",slug),("@title",req.Title.Trim()),("@author",req.Author.Trim()),
            ("@genre",selected[0].name),("@desc",req.Description.Trim()),("@cover",req.CoverUrl.Trim()),
            ("@prefix",AudioStorage.StoryPrefix(slug,id)),("@status",req.Status),("@now",now));
        foreach(var genre in selected) await ExecTx(db,tx,
          "INSERT INTO story_genres(story_id,genre_slug) VALUES(@id,@slug)",("@id",id),("@slug",genre.slug));
        await Helpers.Audit(db,Helpers.UserId(ctx.User),"story.create",id,req.Title,tx);
        await tx.CommitAsync();
    }
    catch(SqliteException ex) when(ex.SqliteErrorCode==19){return Results.Conflict(new {error="Slug hoặc thông tin truyện đã tồn tại."});}
    return Results.Ok(new {id,slug});
});
admin.MapPut("/stories/{id}", async Task<IResult> (string id, StoryReq req, HttpContext ctx, Database source) =>
{
    var err=ValidateStory(req);
    if(err is not null) return Bad(err);
    await using var db=await source.OpenAsync();
    var selected=await ResolveGenresAsync(db,req,id);
    if(selected is null) return Bad("Chọn ít nhất một thể loại đang hoạt động.");
    await using var tx=(SqliteTransaction)await db.BeginTransactionAsync();
    var n=await ExecTx(db,tx,@"UPDATE stories SET title=@title,author=@author,genre=@genre,
        description=@desc,cover_url=@cover,status=@status,updated_at=@now WHERE id=@id",
        ("@title",req.Title.Trim()),("@author",req.Author.Trim()),("@genre",selected[0].name),
        ("@desc",req.Description.Trim()),("@cover",req.CoverUrl.Trim()),
        ("@status",req.Status),("@now",Helpers.Now()),("@id",id));
    if(n==0)return Results.NotFound();
    await ExecTx(db,tx,"DELETE FROM story_genres WHERE story_id=@id",("@id",id));
    foreach(var genre in selected) await ExecTx(db,tx,"INSERT INTO story_genres(story_id,genre_slug) VALUES(@id,@slug)",
      ("@id",id),("@slug",genre.slug));
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"story.update",id,req.Title,tx);
    await tx.CommitAsync();
    return Results.Ok(new {message="Đã lưu truyện. Slug cũ giữ nguyên để không làm hỏng liên kết."});
});
// Upload a cover as PNG/JPG/WebP; stored in the same R2 prefix as the story's episodes.
admin.MapPost("/stories/{id}/cover", async Task<IResult> (string id, HttpRequest request, HttpContext ctx, Database source, AudioStorage storage, StorageJanitor janitor) =>
{
    if (!request.HasFormContentType) return Bad("Yêu cầu multipart/form-data.");
    var form = await request.ReadFormAsync(ctx.RequestAborted);
    var uploaded = form.Files.GetFile("file");
    if (uploaded is null || uploaded.Length < 12 || uploaded.Length > 8L*1024*1024)
        return Bad("Ảnh bìa JPG, PNG hoặc WebP, tối đa 8MB.");
    var ext = Path.GetExtension(uploaded.FileName).ToLowerInvariant();
    if (ext == ".jpeg") ext = ".jpg";
    if (ext is not (".png" or ".jpg" or ".webp")) return Bad("Chỉ hỗ trợ JPG, PNG, WebP.");
    await using var db = await source.OpenAsync();
    var story = await db.FirstAsync("SELECT storage_prefix,cover_file FROM stories WHERE id=@id",("@id",id));
    if (story is null) return Results.NotFound();
    var tmp = Path.Combine(source.DataRoot,"upload-temp",Helpers.Id()+ext);
    Directory.CreateDirectory(Path.GetDirectoryName(tmp)!);
    string saved = "";
    bool savedInDatabase = false;
    try
    {
        await using (var output = new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
            await uploaded.CopyToAsync(output,ctx.RequestAborted);
        var signature = new byte[12];
        await using (var input = File.OpenRead(tmp)) await input.ReadExactlyAsync(signature,ctx.RequestAborted);
        var valid = ext switch
        {
            ".jpg" => signature[0]==0xFF && signature[1]==0xD8 && signature[2]==0xFF,
            ".png" => signature.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}),
            ".webp" => signature.AsSpan(0,4).SequenceEqual("RIFF"u8) &&
                       signature.AsSpan(8,4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid) return Bad("Nội dung file ảnh không khớp với định dạng.");
        saved = await storage.SaveCoverAsync(story.S("storage_prefix"),tmp,ext,ctx.RequestAborted);
        // Commit the new reference and the cleanup task atomically.
        // An R2 failure to delete the old cover never loses the cleanup task.
        await using (var tx = (SqliteTransaction)await db.BeginTransactionAsync())
        {
            await using var cmd = DbExtensions.Cmd(db,
                "UPDATE stories SET cover_file=@file,cover_url='',updated_at=@now WHERE id=@id AND cover_file=@old",tx,
                ("@file",saved),("@now",Helpers.Now()),("@id",id),("@old",story.S("cover_file")));
            var updated = await cmd.ExecuteNonQueryAsync();
            if (updated != 1)
            {
                await tx.RollbackAsync();
                await storage.DeleteBestEffortAsync(saved);
                return Results.Conflict(new { error="Ảnh bìa vừa được thay đổi ở cửa sổ khác." });
            }
            await StorageJanitor.QueueAsync(db,tx,story.S("cover_file"));
            await Helpers.Audit(db,Helpers.UserId(ctx.User),"story.cover_replace",id,
                $"storage={storage.Mode}; old={(story.S("cover_file").Length>0 ? "queued" : "none")}",tx);
            await tx.CommitAsync();
        }
        savedInDatabase = true;
        // Delete obsolete R2/Local image now, or keep a durable retry queue.
        await janitor.TrySweepAsync();
        return Results.Ok(new { coverUrl = "/api/stories/"+id+"/cover", storage=storage.Mode });
    }
    catch (Exception ex)
    {
        if (!savedInDatabase && saved!="") await storage.DeleteBestEffortAsync(saved);
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("CoverUpload").LogError(ex,"Upload ảnh bìa lỗi");
        return Bad("Không thể lưu ảnh bìa. Kiểm tra file hoặc R2.");
    }
    finally { if (File.Exists(tmp)) File.Delete(tmp); }
}).DisableAntiforgery();

// Safely migrate any cover previously stored in the private Local cover directory.
admin.MapPost("/stories/{id}/cover/migrate-r2", async Task<IResult> (string id, HttpContext ctx, Database source, AudioStorage storage) =>
{
    if (!storage.IsR2) return Bad("Chưa bật Cloudflare R2.");
    await using var db = await source.OpenAsync();
    var story = await db.FirstAsync("SELECT storage_prefix,cover_file FROM stories WHERE id=@id",("@id",id));
    if (story is null) return Results.NotFound();
    if (!story.S("cover_file").StartsWith("cover-local:",StringComparison.Ordinal))
        return Bad("Ảnh bìa không phải file Local cần chuyển.");
    string moved = "";
    bool committed = false;
    try
    {
        moved = await storage.MigrateCoverAsync(story.S("cover_file"),story.S("storage_prefix"),ctx.RequestAborted);
        var count = await db.ExecAsync("UPDATE stories SET cover_file=@new,updated_at=@now WHERE id=@id AND cover_file=@old",
            ("@new",moved),("@now",Helpers.Now()),("@id",id),("@old",story.S("cover_file")));
        if (count != 1) { await storage.DeleteBestEffortAsync(moved); return Results.Conflict(new{error="Ảnh bìa đã thay đổi."}); }
        committed = true;
        await Helpers.Audit(db,Helpers.UserId(ctx.User),"story.cover_migrate_r2",id,"Copied image into Image/; local original retained");
        return Results.Ok(new {message="Đã chuyển ảnh bìa vào Image/ trên Cloudflare. Bản Local được giữ lại."});
    }
    catch(Exception ex)
    {
        if (!committed && moved!="") await storage.DeleteBestEffortAsync(moved);
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("CoverMigration").LogError(ex,"Không thể di chuyển ảnh bìa");
        return Bad("Chuyển ảnh bìa thất bại. Kiểm tra file hoặc R2.");
    }
});

admin.MapGet("/storage", async Task<IResult> (AudioStorage storage, Database source) =>
{
    await using var db = await source.OpenAsync();
    var pending = Convert.ToInt32(await db.ValueAsync("SELECT COUNT(*) FROM storage_cleanup"));
    return Results.Ok(new { mode=storage.Mode, bucket=storage.Bucket, isR2=storage.IsR2, cleanupPending=pending });
});
admin.MapPost("/storage/cleanup", async Task<IResult> (StorageJanitor janitor, CancellationToken ct) =>
{
    var (deleted,pending) = await janitor.SweepAsync(ct);
    return Results.Ok(new { deleted, pending });
});
admin.MapGet("/stories/{id}/episodes", async Task<IResult> (string id, Database source) =>
{
    await using var db = await source.OpenAsync();
    return Results.Ok(await db.ListAsync(@"SELECT id,title,episode_number,duration_seconds,status,created_at,
        CASE WHEN hls_ref <> '' THEN 1 ELSE 0 END AS hls_ready,
        CASE WHEN full_file LIKE 'r2:%' THEN 'R2' ELSE 'Local' END AS storage,
        CASE WHEN full_file LIKE 'r2:stories/%/Episodes/%' THEN 'Grouped' ELSE 'Legacy' END AS organization,
        CASE WHEN full_file LIKE 'r2:%' THEN SUBSTR(full_file,4) ELSE full_file END AS audio_path
        FROM episodes WHERE story_id=@id ORDER BY episode_number",("@id",id)));
});

// Story text files (.md, .txt) stored directly on VPS disk (DataRoot/story-texts/<storyId>/).
// Full Admin-only CRUD + Online Notepad editor + Google Drive 2-way sync.
static string ResolveGoogleCallbackUri(HttpContext ctx, string configuredPublicOrigin)
{
    var host = ctx.Request.Host.Value ?? "";
    if (host.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase))
    {
        return $"{ctx.Request.Scheme}://{host}/api/admin/gdrive/callback";
    }
    return configuredPublicOrigin.TrimEnd('/') + "/api/admin/gdrive/callback";
}

app.MapGet("/api/admin/gdrive/callback", async Task<IResult> (
    string? code, string? state, string? error, HttpContext ctx,
    IMemoryCache cache, Database source, GoogleDriveService gdrive) =>
{
    if (!string.IsNullOrWhiteSpace(error))
        return Results.Redirect("/quan-tri?gdrive_error=" + Uri.EscapeDataString(error));
    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        return Results.Redirect("/quan-tri?gdrive_error=missing_code");

    var cacheKey = "gdrive_oauth_state:" + state;
    if (!cache.TryGetValue(cacheKey, out string? adminUserId) || string.IsNullOrWhiteSpace(adminUserId))
        return Results.Redirect("/quan-tri?gdrive_error=expired_state");
    cache.Remove(cacheKey);

    await using var db = await source.OpenAsync();
    var adminUser = await db.FirstAsync("SELECT id,role,status FROM users WHERE id=@id", ("@id", adminUserId));
    if (adminUser is null || adminUser.S("role") != "Admin" || adminUser.S("status") != "Active")
        return Results.Unauthorized();

    try
    {
        var redirectUri = ResolveGoogleCallbackUri(ctx, publicOrigin);
        await gdrive.ExchangeCodeAsync(code, redirectUri, ctx.RequestAborted);
        await Helpers.Audit(db, adminUserId, "gdrive.connect", adminUserId, "Connected Google Drive OAuth2");
        return Results.Redirect("/quan-tri?gdrive=connected");
    }
    catch (Exception ex)
    {
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GoogleDrive").LogError(ex, "OAuth callback failed");
        return Results.Redirect("/quan-tri?gdrive_error=" + Uri.EscapeDataString(ex.Message));
    }
});

admin.MapGet("/gdrive/status", (GoogleDriveService gdrive) => Results.Ok(gdrive.GetStatus()));

admin.MapPost("/gdrive/config", async Task<IResult> (GDriveConfigReq req, HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    await gdrive.SaveCredentialsConfigAsync(req.ClientId, req.ClientSecret, req.RootFolderId, req.RootFolderName);
    await using var db = await source.OpenAsync();
    await Helpers.Audit(db, Helpers.UserId(ctx.User), "gdrive.config", "", $"rootFolder={req.RootFolderName}");
    return Results.Ok(gdrive.GetStatus());
});

admin.MapPost("/gdrive/disconnect", async Task<IResult> (HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    await gdrive.DisconnectAsync();
    await using var db = await source.OpenAsync();
    await Helpers.Audit(db, Helpers.UserId(ctx.User), "gdrive.disconnect", "", "Disconnected Google Drive");
    return Results.Ok(gdrive.GetStatus());
});

admin.MapGet("/gdrive/connect", (HttpContext ctx, IMemoryCache cache, GoogleDriveService gdrive) =>
{
    var state = Helpers.Id() + Helpers.Id();
    cache.Set("gdrive_oauth_state:" + state, Helpers.UserId(ctx.User), TimeSpan.FromMinutes(15));
    var redirectUri = ResolveGoogleCallbackUri(ctx, publicOrigin);
    var url = gdrive.BuildAuthorizeUrl(redirectUri, state);
    return Results.Redirect(url);
});

admin.MapPost("/gdrive/sync-push", async Task<IResult> (HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!gdrive.IsConnected) return Bad("Chưa kết nối tài khoản Google Drive.");
    try
    {
        var (uploaded, storiesSynced) = await gdrive.SyncPushAllToDriveAsync(ctx.RequestAborted);
        await using var db = await source.OpenAsync();
        await Helpers.Audit(db, Helpers.UserId(ctx.User), "gdrive.sync_push", "", $"uploaded={uploaded}; stories={storiesSynced}");
        return Results.Ok(new { uploaded, storiesSynced, message = $"Đã đồng bộ {uploaded} file chữ của {storiesSynced} bộ truyện từ VPS lên Google Drive." });
    }
    catch (Exception ex)
    {
        return Bad("Đồng bộ lên Google Drive thất bại: " + ex.Message);
    }
});

admin.MapPost("/gdrive/sync-pull", async Task<IResult> (HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!gdrive.IsConnected) return Bad("Chưa kết nối tài khoản Google Drive.");
    try
    {
        var (imported, updated) = await gdrive.SyncPullFromDriveAsync(ctx.RequestAborted);
        await using var db = await source.OpenAsync();
        await Helpers.Audit(db, Helpers.UserId(ctx.User), "gdrive.sync_pull", "", $"imported={imported}; updated={updated}");
        return Results.Ok(new { imported, updated, message = $"Đã kéo từ Google Drive về VPS: thêm mới {imported} file, cập nhật {updated} file." });
    }
    catch (Exception ex)
    {
        return Bad("Đồng bộ từ Google Drive về VPS thất bại: " + ex.Message);
    }
});

admin.MapPost("/gdrive/sync-both", async Task<IResult> (HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!gdrive.IsConnected) return Bad("Chưa kết nối tài khoản Google Drive.");
    try
    {
        var (pushedNew, pushedUpdated, pulledNew, pulledUpdated, storiesSynced) = await gdrive.SyncSmartBothAsync(ctx.RequestAborted);
        await using var db = await source.OpenAsync();
        await Helpers.Audit(db, Helpers.UserId(ctx.User), "gdrive.sync_both", "",
            $"pushedNew={pushedNew}; pushedUpdated={pushedUpdated}; pulledNew={pulledNew}; pulledUpdated={pulledUpdated}; stories={storiesSynced}");
        return Results.Ok(new
        {
            pushedNew,
            pushedUpdated,
            pulledNew,
            pulledUpdated,
            storiesSynced,
            message = $"Đã đồng bộ thông minh 2 chiều (ghi đè bằng bản mới nhất): Đẩy lên Drive +{pushedNew} mới / {pushedUpdated} cập nhật · Kéo về VPS +{pulledNew} mới / {pulledUpdated} cập nhật."
        });
    }
    catch (Exception ex)
    {
        return Bad("Đồng bộ 2 chiều VPS ↔ Google Drive thất bại: " + ex.Message);
    }
});

admin.MapGet("/gdrive/files", async Task<IResult> (string? folderId, HttpContext ctx, GoogleDriveService gdrive) =>
{
    if (!gdrive.IsConnected) return Bad("Chưa kết nối tài khoản Google Drive.");
    try { return Results.Ok(await gdrive.ListFolderItemsAsync(folderId, ctx.RequestAborted)); }
    catch (Exception ex) { return Bad(ex.Message); }
});

admin.MapPost("/gdrive/folders", async Task<IResult> (GDriveFolderCreateReq req, HttpContext ctx, GoogleDriveService gdrive) =>
{
    if (!gdrive.IsConnected) return Bad("Chưa kết nối tài khoản Google Drive.");
    if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Trim().Length > 150) return Bad("Tên thư mục không hợp lệ.");
    try
    {
        var id = await gdrive.CreateSubfolderOnDriveAsync(req.ParentFolderId, req.Name, ctx.RequestAborted);
        return Results.Ok(new { id, name = req.Name.Trim() });
    }
    catch (Exception ex) { return Bad(ex.Message); }
});

admin.MapGet("/gdrive/files/{driveFileId}/content", async Task<IResult> (string driveFileId, HttpContext ctx, GoogleDriveService gdrive) =>
{
    if (!gdrive.IsConnected) return Bad("Chưa kết nối tài khoản Google Drive.");
    try
    {
        var (id, name, desc, size, mod, link, content) = await gdrive.ReadDriveFileTextAsync(driveFileId, ctx.RequestAborted);
        return Results.Ok(new { id, fileName = name, title = desc, sizeBytes = size, updatedAt = mod, webViewLink = link, content });
    }
    catch (Exception ex) { return Bad(ex.Message); }
});

admin.MapPost("/gdrive/files/save", async Task<IResult> (GDriveFileSaveReq req, HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!gdrive.IsConnected) return Bad("Chưa kết nối tài khoản Google Drive.");
    try
    {
        var (id, name, size) = await gdrive.SaveDriveFileDirectAsync(
            req.DriveFileId, req.ParentFolderId, req.FileName, req.Title, req.Content, ctx.RequestAborted);
        await using var db = await source.OpenAsync();
        await Helpers.Audit(db, Helpers.UserId(ctx.User), "gdrive.file_save", id, $"name={name}; size={size}");
        return Results.Ok(new { id, fileName = name, sizeBytes = size, updatedAt = Helpers.Now() });
    }
    catch (Exception ex) { return Bad(ex.Message); }
});

admin.MapDelete("/gdrive/files/{driveFileId}", async Task<IResult> (string driveFileId, HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!gdrive.IsConnected) return Bad("Chưa kết nối tài khoản Google Drive.");
    try
    {
        await gdrive.DeleteDriveItemAsync(driveFileId, ctx.RequestAborted);
        await using var db = await source.OpenAsync();
        await db.ExecAsync("UPDATE story_text_files SET drive_file_id='' WHERE drive_file_id=@did", ("@did", driveFileId));
        await Helpers.Audit(db, Helpers.UserId(ctx.User), "gdrive.file_delete", driveFileId, "Moved Drive file to Trash");
        return Results.Ok(new { message = "Đã chuyển file/thư mục trên Google Drive vào Thùng rác." });
    }
    catch (Exception ex) { return Bad(ex.Message); }
});

static string NormalizeStorageTarget(string? raw, string fallback = "vps")
{
    var s = (raw ?? "").Trim().ToLowerInvariant();
    return s is "drive" or "both" or "vps" ? s : fallback;
}

static string ComputeRowStorageTarget(Dictionary<string, object?> row)
{
    var hasLocal = !string.IsNullOrWhiteSpace(row.S("stored_name"));
    var hasDrive = !string.IsNullOrWhiteSpace(row.S("drive_file_id"));
    if (hasLocal && hasDrive) return "both";
    if (!hasLocal && hasDrive) return "drive";
    var raw = row.S("storage_target");
    return raw is "drive" or "both" ? raw : "vps";
}

admin.MapGet("/texts", async Task<IResult> (string? storyId, Database source) =>
{
    await using var db = await source.OpenAsync();
    if (!string.IsNullOrWhiteSpace(storyId))
    {
        if (!Regex.IsMatch(storyId, "^[a-f0-9]{32}$")) return Results.NotFound();
        return Results.Ok(await db.ListAsync(@"SELECT tf.id,tf.story_id,tf.title,tf.file_name,tf.extension,tf.size_bytes,tf.drive_file_id,
            CASE
              WHEN tf.stored_name<>'' AND tf.drive_file_id<>'' THEN 'both'
              WHEN tf.storage_target='drive' OR (tf.stored_name='' AND tf.drive_file_id<>'') THEN 'drive'
              ELSE 'vps'
            END AS storage_target,
            tf.created_at,
            CASE WHEN tf.updated_at<>'' THEN tf.updated_at ELSE tf.created_at END AS updated_at,
            s.title AS story_title, s.slug AS story_slug
            FROM story_text_files tf JOIN stories s ON s.id=tf.story_id
            WHERE tf.story_id=@sid
            ORDER BY COALESCE(NULLIF(tf.updated_at,''), tf.created_at) DESC, tf.file_name ASC", ("@sid", storyId)));
    }
    return Results.Ok(await db.ListAsync(@"SELECT tf.id,tf.story_id,tf.title,tf.file_name,tf.extension,tf.size_bytes,tf.drive_file_id,
        CASE
          WHEN tf.stored_name<>'' AND tf.drive_file_id<>'' THEN 'both'
          WHEN tf.storage_target='drive' OR (tf.stored_name='' AND tf.drive_file_id<>'') THEN 'drive'
          ELSE 'vps'
        END AS storage_target,
        tf.created_at,
        CASE WHEN tf.updated_at<>'' THEN tf.updated_at ELSE tf.created_at END AS updated_at,
        s.title AS story_title, s.slug AS story_slug
        FROM story_text_files tf JOIN stories s ON s.id=tf.story_id
        ORDER BY COALESCE(NULLIF(tf.updated_at,''), tf.created_at) DESC, tf.file_name ASC LIMIT 1000"));
});

admin.MapGet("/stories/{id}/texts", async Task<IResult> (string id, Database source) =>
{
    if (!Regex.IsMatch(id, "^[a-f0-9]{32}$")) return Results.NotFound();
    await using var db = await source.OpenAsync();
    return Results.Ok(await db.ListAsync(@"SELECT tf.id,tf.story_id,tf.title,tf.file_name,tf.extension,tf.size_bytes,tf.drive_file_id,
        CASE
          WHEN tf.stored_name<>'' AND tf.drive_file_id<>'' THEN 'both'
          WHEN tf.storage_target='drive' OR (tf.stored_name='' AND tf.drive_file_id<>'') THEN 'drive'
          ELSE 'vps'
        END AS storage_target,
        tf.created_at,
        CASE WHEN tf.updated_at<>'' THEN tf.updated_at ELSE tf.created_at END AS updated_at,
        s.title AS story_title
        FROM story_text_files tf JOIN stories s ON s.id=tf.story_id
        WHERE tf.story_id=@id ORDER BY COALESCE(NULLIF(tf.updated_at,''), tf.created_at) DESC, tf.file_name ASC", ("@id", id)));
});

admin.MapPost("/stories/{id}/texts/create", async Task<IResult> (string id, TextCreateReq req, HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!Regex.IsMatch(id, "^[a-f0-9]{32}$")) return Results.NotFound();
    var target = NormalizeStorageTarget(req.StorageTarget, "vps");
    if (target is "drive" or "both" && !gdrive.IsConnected)
        return Bad("Chưa kết nối tài khoản Google Drive. Hãy kết nối Google Drive trước hoặc chọn lưu trên VPS.");

    var ext = (req.Extension ?? ".txt").Trim().ToLowerInvariant();
    if (!ext.StartsWith('.')) ext = "." + ext;
    if (ext is not (".md" or ".txt")) return Bad("Định dạng file phải là .md hoặc .txt.");

    var rawTitle = (req.Title ?? "").Trim();
    var rawName = Path.GetFileName((req.FileName ?? "").Trim());
    if (string.IsNullOrWhiteSpace(rawName))
        rawName = (rawTitle != "" ? Slugs.FromTitle(rawTitle) : "ban-thao-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss")) + ext;
    if (!rawName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        rawName = Path.GetFileNameWithoutExtension(rawName) + ext;
    if (rawName.Length is < 4 or > 200) return Bad("Tên file không hợp lệ.");
    if (rawTitle == "") rawTitle = Path.GetFileNameWithoutExtension(rawName);
    if (rawTitle.Length > 180) rawTitle = rawTitle[..180];

    var body = req.Content ?? "";
    if (body.Length > 25 * 1024 * 1024) return Bad("Nội dung văn bản vượt quá 25MB.");

    await using var db = await source.OpenAsync();
    var storyExists = await db.ValueAsync("SELECT 1 FROM stories WHERE id=@id", ("@id", id));
    if (storyExists is null) return Results.NotFound();

    var existing = await db.FirstAsync(
        "SELECT * FROM story_text_files WHERE story_id=@sid AND LOWER(file_name)=LOWER(@fname) ORDER BY updated_at DESC LIMIT 1",
        ("@sid", id), ("@fname", rawName));

    var textId = existing is not null ? existing.S("id") : Helpers.Id();
    var utf8NoBom = new System.Text.UTF8Encoding(false);
    var bytes = utf8NoBom.GetBytes(body);
    var actualSize = (long)bytes.Length;
    var now = Helpers.Now();
    var storedName = existing?.S("stored_name") ?? "";
    var driveFileId = existing?.S("drive_file_id") ?? "";
    var finalTarget = target;
    if (existing is not null)
    {
        var prevTarget = ComputeRowStorageTarget(existing);
        if ((prevTarget == "both") || (prevTarget == "vps" && target == "drive") || (prevTarget == "drive" && target == "vps"))
            finalTarget = "both";
    }

    var localDestPath = "";
    if (finalTarget is "vps" or "both")
    {
        var storyDir = Path.Combine(source.TextRoot, id);
        Directory.CreateDirectory(storyDir);
        if (string.IsNullOrWhiteSpace(storedName)) storedName = textId + ext;
        localDestPath = Path.Combine(storyDir, storedName);
        await File.WriteAllBytesAsync(localDestPath, bytes, ctx.RequestAborted);
        actualSize = new FileInfo(localDestPath).Length;
    }

    if (finalTarget is "drive" or "both")
    {
        try
        {
            var folderId = await gdrive.EnsureStoryFolderAsync(db, id, ctx.RequestAborted);
            var (savedDriveId, driveMod) = await gdrive.UpsertTextFileOnDriveWithMetaAsync(folderId, driveFileId, rawName, rawTitle, bytes, ext, ctx.RequestAborted);
            driveFileId = savedDriveId;
            if (!string.IsNullOrWhiteSpace(driveMod)) now = driveMod;
        }
        catch (Exception ex)
        {
            if (existing is null && localDestPath != "" && File.Exists(localDestPath))
            {
                try { File.Delete(localDestPath); } catch { }
            }
            return Bad("Không thể lưu file trên Google Drive: " + ex.Message);
        }
    }

    if (localDestPath != "")
        GoogleDriveService.AlignLocalFileTimestamp(localDestPath, now);

    if (existing is null)
    {
        await db.ExecAsync(@"INSERT INTO story_text_files(id,story_id,title,file_name,stored_name,extension,size_bytes,drive_file_id,storage_target,created_at,updated_at)
            VALUES(@id,@sid,@title,@fname,@sname,@ext,@size,@did,@target,@now,@now)",
            ("@id", textId), ("@sid", id), ("@title", rawTitle), ("@fname", rawName),
            ("@sname", storedName), ("@ext", ext), ("@size", actualSize), ("@did", driveFileId), ("@target", finalTarget), ("@now", now));
    }
    else
    {
        await db.ExecAsync(@"UPDATE story_text_files
            SET title=@title, file_name=@fname, stored_name=@sname, extension=@ext, size_bytes=@size, drive_file_id=@did, storage_target=@target, updated_at=@now
            WHERE id=@id",
            ("@title", rawTitle), ("@fname", rawName), ("@sname", storedName), ("@ext", ext),
            ("@size", actualSize), ("@did", driveFileId), ("@target", finalTarget), ("@now", now), ("@id", textId));
    }

    await Helpers.Audit(db, Helpers.UserId(ctx.User), "story.text_create", id,
        $"textId={textId}; file={rawName}; size={actualSize}; target={finalTarget}");
    return Results.Ok(new
    {
        id = textId,
        story_id = id,
        title = rawTitle,
        file_name = rawName,
        extension = ext,
        size_bytes = actualSize,
        drive_file_id = driveFileId,
        storage_target = finalTarget,
        created_at = existing?.S("created_at") ?? now,
        updated_at = now
    });
});

admin.MapPost("/stories/{id}/texts", async Task<IResult> (string id, HttpRequest request, HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!Regex.IsMatch(id, "^[a-f0-9]{32}$")) return Results.NotFound();
    if (!request.HasFormContentType) return Bad("Yêu cầu multipart/form-data.");
    var form = await request.ReadFormAsync(ctx.RequestAborted);
    var noteTitle = form["title"].ToString().Trim();
    var target = NormalizeStorageTarget(form["storageTarget"].ToString(), "vps");
    if (target is "drive" or "both" && !gdrive.IsConnected)
        return Bad("Chưa kết nối tài khoản Google Drive. Hãy kết nối Google Drive trước khi upload lên Drive.");
    if (noteTitle.Length > 180) return Bad("Ghi chú / tiêu đề tối đa 180 ký tự.");
    if (form.Files.Count == 0) return Bad("Hãy chọn ít nhất 1 file .md hoặc .txt.");
    if (form.Files.Count > 100) return Bad("Mỗi lần upload tối đa 100 file chữ.");

    await using var db = await source.OpenAsync();
    var storyExists = await db.ValueAsync("SELECT 1 FROM stories WHERE id=@id", ("@id", id));
    if (storyExists is null) return Results.NotFound();

    const long maxBytesPerFile = 50L * 1024 * 1024; // 50 MB per .md/.txt file
    foreach (var f in form.Files)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        if (ext is not (".md" or ".txt"))
            return Bad($"File '{f.FileName}' không hợp lệ. Chỉ chấp nhận định dạng .md hoặc .txt.");
        if (f.Length <= 0 || f.Length > maxBytesPerFile)
            return Bad($"File '{f.FileName}' phải lớn hơn 0 byte và tối đa 50MB.");
    }

    var storyDir = Path.Combine(source.TextRoot, id);
    Directory.CreateDirectory(storyDir);
    var createdItems = new List<object>();
    var writtenFiles = new List<string>();
    try
    {
        string driveFolderId = "";
        if (target is "drive" or "both" || gdrive.IsConnected)
        {
            try
            {
                if (gdrive.IsConnected)
                    driveFolderId = await gdrive.EnsureStoryFolderAsync(db, id, ctx.RequestAborted);
            }
            catch
            {
                if (target is "drive" or "both") throw;
            }
        }

        foreach (var f in form.Files)
        {
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            var rawName = Path.GetFileName(f.FileName).Trim();
            if (rawName.Length > 200) rawName = rawName[^200..];

            var existing = await db.FirstAsync(
                "SELECT * FROM story_text_files WHERE story_id=@sid AND LOWER(file_name)=LOWER(@fname) ORDER BY updated_at DESC LIMIT 1",
                ("@sid", id), ("@fname", rawName));

            var textId = existing is not null ? existing.S("id") : Helpers.Id();
            var itemTitle = noteTitle != ""
                ? (form.Files.Count > 1 ? $"{noteTitle} ({Path.GetFileNameWithoutExtension(rawName)})" : noteTitle)
                : (existing is not null && !string.IsNullOrWhiteSpace(existing.S("title")) ? existing.S("title") : Path.GetFileNameWithoutExtension(rawName));
            if (itemTitle.Length > 180) itemTitle = itemTitle[..180];
            var now = Helpers.Now();

            var storedName = existing?.S("stored_name") ?? "";
            var driveFileId = existing?.S("drive_file_id") ?? "";
            var itemTarget = target;
            if (existing is not null)
            {
                var prevTarget = ComputeRowStorageTarget(existing);
                // If the file was already synced on both or existed on the other side, overwrite & keep both updated!
                if (prevTarget == "both" || (prevTarget == "vps" && target == "drive") || (prevTarget == "drive" && target == "vps"))
                    itemTarget = "both";
            }

            long actualSize = 0;

            using var ms = new MemoryStream((int)Math.Min(f.Length, maxBytesPerFile));
            await f.CopyToAsync(ms, ctx.RequestAborted);
            var fileBytes = ms.ToArray();
            if (fileBytes.Length >= 2 && ((fileBytes[0] == 'M' && fileBytes[1] == 'Z') ||
                (fileBytes.Length >= 4 && fileBytes[0] == 0x7F && fileBytes[1] == 'E' && fileBytes[2] == 'L' && fileBytes[3] == 'F')))
            {
                throw new InvalidDataException($"File '{rawName}' là file thực thi nhị phân, không phải văn bản .md/.txt.");
            }
            actualSize = fileBytes.Length;

            var destPath = "";
            if (itemTarget is "vps" or "both")
            {
                if (string.IsNullOrWhiteSpace(storedName)) storedName = textId + ext;
                destPath = Path.Combine(storyDir, storedName);
                await File.WriteAllBytesAsync(destPath, fileBytes, ctx.RequestAborted);
                if (existing is null) writtenFiles.Add(destPath);
            }

            if (itemTarget is "drive" or "both")
            {
                if (string.IsNullOrWhiteSpace(driveFolderId))
                    driveFolderId = await gdrive.EnsureStoryFolderAsync(db, id, ctx.RequestAborted);
                var (savedDriveId, driveMod) = await gdrive.UpsertTextFileOnDriveWithMetaAsync(driveFolderId, driveFileId, rawName, itemTitle, fileBytes, ext, ctx.RequestAborted);
                driveFileId = savedDriveId;
                if (!string.IsNullOrWhiteSpace(driveMod)) now = driveMod;
            }

            if (destPath != "")
                GoogleDriveService.AlignLocalFileTimestamp(destPath, now);

            if (existing is null)
            {
                await db.ExecAsync(@"INSERT INTO story_text_files(id,story_id,title,file_name,stored_name,extension,size_bytes,drive_file_id,storage_target,created_at,updated_at)
                    VALUES(@id,@sid,@title,@fname,@sname,@ext,@size,@did,@target,@now,@now)",
                    ("@id", textId), ("@sid", id), ("@title", itemTitle), ("@fname", rawName),
                    ("@sname", storedName), ("@ext", ext), ("@size", actualSize), ("@did", driveFileId), ("@target", itemTarget), ("@now", now));
            }
            else
            {
                await db.ExecAsync(@"UPDATE story_text_files
                    SET title=@title, file_name=@fname, stored_name=@sname, extension=@ext, size_bytes=@size, drive_file_id=@did, storage_target=@target, updated_at=@now
                    WHERE id=@id",
                    ("@title", itemTitle), ("@fname", rawName), ("@sname", storedName), ("@ext", ext),
                    ("@size", actualSize), ("@did", driveFileId), ("@target", itemTarget), ("@now", now), ("@id", textId));
            }

            createdItems.Add(new
            {
                id = textId,
                story_id = id,
                title = itemTitle,
                file_name = rawName,
                extension = ext,
                size_bytes = actualSize,
                drive_file_id = driveFileId,
                storage_target = itemTarget,
                created_at = existing?.S("created_at") ?? now,
                updated_at = now
            });
        }

        await Helpers.Audit(db, Helpers.UserId(ctx.User), "story.text_upload", id,
            $"target={target}; count={createdItems.Count}; files={string.Join(", ", form.Files.Select(x => Path.GetFileName(x.FileName)))}");
        return Results.Ok(new { count = createdItems.Count, storageTarget = target, items = createdItems });
    }
    catch (Exception ex)
    {
        foreach (var p in writtenFiles)
        {
            if (File.Exists(p))
            {
                try { File.Delete(p); } catch { }
            }
        }
        if (ex is InvalidDataException) return Bad(ex.Message);
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("TextUpload").LogError(ex, "Không thể lưu file chữ");
        return Bad(target is "drive" or "both"
            ? "Upload file chữ (.md, .txt) lên Google Drive thất bại: " + ex.Message
            : "Lưu file chữ (.md, .txt) lên VPS thất bại.");
    }
}).DisableAntiforgery();

admin.MapGet("/texts/{textId}/content", async Task<IResult> (string textId, HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!Regex.IsMatch(textId, "^[a-f0-9]{32}$")) return Results.NotFound();
    await using var db = await source.OpenAsync();
    var row = await db.FirstAsync(@"SELECT tf.*, s.title AS story_title
        FROM story_text_files tf JOIN stories s ON s.id=tf.story_id
        WHERE tf.id=@id", ("@id", textId));
    if (row is null) return Results.NotFound();

    var storedName = row.S("stored_name");
    var driveId = row.S("drive_file_id");
    var storageTarget = ComputeRowStorageTarget(row);
    var localPath = !string.IsNullOrWhiteSpace(storedName)
        ? Path.Combine(source.TextRoot, row.S("story_id"), storedName)
        : "";
    var hasLocal = localPath != "" && File.Exists(localPath);

    // If stored on BOTH VPS and Drive, check if Google Drive has a newer version and sync it before returning!
    if (hasLocal && !string.IsNullOrWhiteSpace(driveId) && gdrive.IsConnected)
    {
        try
        {
            var driveMeta = await gdrive.GetDriveFileMetaPublicAsync(driveId, ctx.RequestAborted);
            if (driveMeta is not null && !driveMeta.Value.Trashed)
            {
                var vpsTimeUtc = GoogleDriveService.GetVpsTimestampUtc(row, localPath);
                var driveTimeUtc = GoogleDriveService.ParseTimestampUtc(driveMeta.Value.ModifiedTime);
                if ((driveTimeUtc - vpsTimeUtc).TotalSeconds > 2.0)
                {
                    var (_, _, _, _, driveMod, _, driveContent) = await gdrive.ReadDriveFileTextAsync(driveId, ctx.RequestAborted);
                    var driveBytes = new System.Text.UTF8Encoding(false).GetBytes(driveContent);
                    await File.WriteAllBytesAsync(localPath, driveBytes, ctx.RequestAborted);
                    var syncIso = !string.IsNullOrWhiteSpace(driveMod) ? driveMod : driveMeta.Value.ModifiedTime;
                    GoogleDriveService.AlignLocalFileTimestamp(localPath, syncIso);
                    await db.ExecAsync("UPDATE story_text_files SET size_bytes=@size, updated_at=@now WHERE id=@id",
                        ("@size", driveBytes.Length), ("@now", syncIso), ("@id", textId));

                    return Results.Ok(new
                    {
                        id = row.S("id"),
                        storyId = row.S("story_id"),
                        storyTitle = row.S("story_title"),
                        title = row.S("title"),
                        fileName = row.S("file_name"),
                        extension = row.S("extension"),
                        sizeBytes = driveBytes.Length,
                        driveFileId = driveId,
                        storageTarget,
                        createdAt = row.S("created_at"),
                        updatedAt = syncIso,
                        truncated = false,
                        content = driveContent
                    });
                }
            }
        }
        catch { }
    }

    if (!hasLocal)
    {
        if (string.IsNullOrWhiteSpace(driveId))
            return Results.NotFound(new { error = "Không tìm thấy file chữ trên VPS hoặc Google Drive." });
        if (!gdrive.IsConnected)
            return Bad("File này được lưu trên Google Drive. Vui lòng kết nối tài khoản Google Drive để đọc/sửa.");
        try
        {
            var (_, _, _, driveSize, driveMod, _, driveContent) = await gdrive.ReadDriveFileTextAsync(driveId, ctx.RequestAborted);
            return Results.Ok(new
            {
                id = row.S("id"),
                storyId = row.S("story_id"),
                storyTitle = row.S("story_title"),
                title = row.S("title"),
                fileName = row.S("file_name"),
                extension = row.S("extension"),
                sizeBytes = driveSize > 0 ? driveSize : row.N("size_bytes"),
                driveFileId = driveId,
                storageTarget,
                createdAt = row.S("created_at"),
                updatedAt = !string.IsNullOrWhiteSpace(driveMod) ? driveMod : (row.S("updated_at") != "" ? row.S("updated_at") : row.S("created_at")),
                truncated = false,
                content = driveContent
            });
        }
        catch (Exception ex)
        {
            return Bad("Không đọc được file từ Google Drive: " + ex.Message);
        }
    }

    const int maxEditBytes = 25 * 1024 * 1024; // Up to 25 MB editable in Online Notepad
    var info = new FileInfo(localPath);
    await using var fs = File.OpenRead(localPath);
    var toRead = (int)Math.Min(info.Length, maxEditBytes);
    var buffer = new byte[toRead];
    var read = await fs.ReadAsync(buffer.AsMemory(0, toRead), ctx.RequestAborted);
    using var ms2 = new MemoryStream(buffer, 0, read);
    using var reader = new StreamReader(ms2, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
    var content = await reader.ReadToEndAsync(ctx.RequestAborted);

    return Results.Ok(new
    {
        id = row.S("id"),
        storyId = row.S("story_id"),
        storyTitle = row.S("story_title"),
        title = row.S("title"),
        fileName = row.S("file_name"),
        extension = row.S("extension"),
        sizeBytes = (int)info.Length,
        driveFileId = row.S("drive_file_id"),
        storageTarget,
        createdAt = row.S("created_at"),
        updatedAt = row.S("updated_at") != "" ? row.S("updated_at") : row.S("created_at"),
        truncated = info.Length > maxEditBytes,
        content
    });
});

admin.MapPut("/texts/{textId}", async Task<IResult> (string textId, TextUpdateReq req, HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!Regex.IsMatch(textId, "^[a-f0-9]{32}$")) return Results.NotFound();
    await using var db = await source.OpenAsync();
    var old = await db.FirstAsync("SELECT * FROM story_text_files WHERE id=@id", ("@id", textId));
    if (old is null) return Results.NotFound();

    var oldTarget = ComputeRowStorageTarget(old);
    var target = string.IsNullOrWhiteSpace(req.StorageTarget)
        ? oldTarget
        : NormalizeStorageTarget(req.StorageTarget, oldTarget);

    if (target is "drive" or "both" && !gdrive.IsConnected)
        return Bad("Chưa kết nối tài khoản Google Drive. Hãy kết nối Google Drive trước khi lưu lên Drive.");

    var targetStoryId = string.IsNullOrWhiteSpace(req.StoryId) ? old.S("story_id") : req.StoryId.Trim();
    if (!Regex.IsMatch(targetStoryId, "^[a-f0-9]{32}$")) return Bad("Bộ truyện không hợp lệ.");
    if (await db.ValueAsync("SELECT 1 FROM stories WHERE id=@id", ("@id", targetStoryId)) is null)
        return Bad("Không tìm thấy bộ truyện đích.");

    var ext = string.IsNullOrWhiteSpace(req.Extension) ? old.S("extension") : req.Extension.Trim().ToLowerInvariant();
    if (!ext.StartsWith('.')) ext = "." + ext;
    if (ext is not (".md" or ".txt")) return Bad("Định dạng file phải là .md hoặc .txt.");

    var rawName = Path.GetFileName((req.FileName ?? old.S("file_name")).Trim());
    if (string.IsNullOrWhiteSpace(rawName)) rawName = textId + ext;
    if (!rawName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        rawName = Path.GetFileNameWithoutExtension(rawName) + ext;
    if (rawName.Length is < 4 or > 200) return Bad("Tên file không hợp lệ.");

    var rawTitle = (req.Title ?? "").Trim();
    if (rawTitle == "") rawTitle = Path.GetFileNameWithoutExtension(rawName);
    if (rawTitle.Length > 180) rawTitle = rawTitle[..180];

    var body = req.Content ?? "";
    if (body.Length > 25 * 1024 * 1024) return Bad("Nội dung văn bản vượt quá giới hạn 25MB.");

    var utf8NoBom = new System.Text.UTF8Encoding(false);
    var bytes = utf8NoBom.GetBytes(body);
    long actualSize = bytes.Length;
    var newStoredName = "";
    var driveFileId = old.S("drive_file_id");
    var oldStoredName = old.S("stored_name");
    var oldPath = !string.IsNullOrWhiteSpace(oldStoredName)
        ? Path.Combine(source.TextRoot, old.S("story_id"), oldStoredName)
        : "";
    var newPath = "";
    var now = Helpers.Now();

    if (target is "vps" or "both")
    {
        var targetDir = Path.Combine(source.TextRoot, targetStoryId);
        Directory.CreateDirectory(targetDir);
        newStoredName = textId + ext;
        newPath = Path.Combine(targetDir, newStoredName);
        var tempPath = newPath + ".tmp-" + Guid.NewGuid().ToString("N")[..8];

        await File.WriteAllBytesAsync(tempPath, bytes, ctx.RequestAborted);
        File.Move(tempPath, newPath, overwrite: true);
        if (oldPath != "" && !string.Equals(oldPath, newPath, StringComparison.Ordinal) && File.Exists(oldPath))
        {
            try { File.Delete(oldPath); } catch { }
        }
        actualSize = new FileInfo(newPath).Length;
    }

    if (target is "drive" or "both")
    {
        try
        {
            var folderId = await gdrive.EnsureStoryFolderAsync(db, targetStoryId, ctx.RequestAborted);
            var (savedDriveId, driveMod) = await gdrive.UpsertTextFileOnDriveWithMetaAsync(folderId, driveFileId, rawName, rawTitle, bytes, ext, ctx.RequestAborted);
            driveFileId = savedDriveId;
            if (!string.IsNullOrWhiteSpace(driveMod)) now = driveMod;
        }
        catch (Exception ex)
        {
            return Bad("Lưu file lên Google Drive thất bại: " + ex.Message);
        }
    }

    if (newPath != "")
        GoogleDriveService.AlignLocalFileTimestamp(newPath, now);

    if (target == "drive" && oldPath != "" && File.Exists(oldPath))
    {
        try { File.Delete(oldPath); } catch { }
    }
    else if (target == "vps")
    {
        if (!string.IsNullOrWhiteSpace(driveFileId) && gdrive.IsConnected)
        {
            try { await gdrive.DeleteDriveItemAsync(driveFileId, ctx.RequestAborted); } catch { }
        }
        driveFileId = "";
    }

    await db.ExecAsync(@"UPDATE story_text_files
        SET story_id=@sid, title=@title, file_name=@fname, stored_name=@sname, extension=@ext, size_bytes=@size, drive_file_id=@did, storage_target=@target, updated_at=@now
        WHERE id=@id",
        ("@sid", targetStoryId), ("@title", rawTitle), ("@fname", rawName),
        ("@sname", newStoredName), ("@ext", ext), ("@size", actualSize), ("@did", driveFileId), ("@target", target), ("@now", now), ("@id", textId));

    await Helpers.Audit(db, Helpers.UserId(ctx.User), "story.text_update", targetStoryId,
        $"textId={textId}; file={rawName}; size={actualSize}; target={target}");
    return Results.Ok(new
    {
        message = target == "both"
            ? "Đã lưu & ghi đè nội dung file mới nhất lên cả VPS và Google Drive (Dự phòng)."
            : (target == "drive" ? "Đã lưu nội dung file chữ lên Google Drive." : "Đã lưu nội dung file chữ lên VPS."),
        id = textId,
        storyId = targetStoryId,
        title = rawTitle,
        fileName = rawName,
        extension = ext,
        sizeBytes = actualSize,
        driveFileId,
        storageTarget = target,
        updatedAt = now
    });
});

admin.MapPost("/texts/{textId}/transfer", async Task<IResult> (string textId, TextTransferReq req, HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!Regex.IsMatch(textId, "^[a-f0-9]{32}$")) return Results.NotFound();
    var target = NormalizeStorageTarget(req.Target, "both");
    await using var db = await source.OpenAsync();
    var row = await db.FirstAsync("SELECT * FROM story_text_files WHERE id=@id", ("@id", textId));
    if (row is null) return Results.NotFound();

    var storyId = row.S("story_id");
    var fileName = row.S("file_name");

    try
    {
        var (storedName, driveId, sizeBytes, updatedAt, winnerSource) = await gdrive.SyncOrTransferSingleFileAsync(db, row, target, ctx.RequestAborted);
        await Helpers.Audit(db, Helpers.UserId(ctx.User), "story.text_transfer", storyId,
            $"textId={textId}; file={fileName}; target={target}; winner={winnerSource}");
        var winnerLabel = winnerSource == "drive" ? "Google Drive" : "VPS";
        return Results.Ok(new
        {
            id = textId,
            storage_target = target,
            storageTarget = target,
            driveFileId = driveId,
            sizeBytes,
            updatedAt,
            winnerSource,
            message = target == "both"
                ? $"Đã đồng bộ dự phòng '{fileName}' (sử dụng bản cập nhật mới nhất từ {winnerLabel} để ghi đè)."
                : (target == "drive"
                    ? $"Đã chuyển '{fileName}' sang Google Drive (sử dụng bản mới nhất từ {winnerLabel})."
                    : $"Đã chuyển '{fileName}' về VPS (sử dụng bản mới nhất từ {winnerLabel}).")
        });
    }
    catch (Exception ex)
    {
        return Bad("Đồng bộ / chuyển đổi file thất bại: " + ex.Message);
    }
});

admin.MapGet("/texts/{textId}/download", async Task<IResult> (string textId, HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!Regex.IsMatch(textId, "^[a-f0-9]{32}$")) return Results.NotFound();
    await using var db = await source.OpenAsync();
    var row = await db.FirstAsync("SELECT * FROM story_text_files WHERE id=@id", ("@id", textId));
    if (row is null) return Results.NotFound();
    var contentType = row.S("extension") == ".md" ? "text/markdown; charset=utf-8" : "text/plain; charset=utf-8";
    var storedName = row.S("stored_name");
    if (!string.IsNullOrWhiteSpace(storedName))
    {
        var path = Path.Combine(source.TextRoot, row.S("story_id"), storedName);
        if (File.Exists(path))
            return Results.File(path, contentType, fileDownloadName: row.S("file_name"));
    }
    var driveId = row.S("drive_file_id");
    if (!string.IsNullOrWhiteSpace(driveId) && gdrive.IsConnected)
    {
        try
        {
            var bytes = await gdrive.DownloadDriveFileBytesAsync(driveId, ctx.RequestAborted);
            return Results.File(bytes, contentType, fileDownloadName: row.S("file_name"));
        }
        catch { }
    }
    return Results.NotFound();
});

admin.MapDelete("/texts/{textId}", async Task<IResult> (string textId, HttpContext ctx, Database source, GoogleDriveService gdrive) =>
{
    if (!Regex.IsMatch(textId, "^[a-f0-9]{32}$")) return Results.NotFound();
    await using var db = await source.OpenAsync();
    var row = await db.FirstAsync("SELECT * FROM story_text_files WHERE id=@id", ("@id", textId));
    if (row is null) return Results.NotFound();
    await db.ExecAsync("DELETE FROM story_text_files WHERE id=@id", ("@id", textId));
    var storedName = row.S("stored_name");
    if (!string.IsNullOrWhiteSpace(storedName))
    {
        var path = Path.Combine(source.TextRoot, row.S("story_id"), storedName);
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }
    var driveId = row.S("drive_file_id");
    if (!string.IsNullOrWhiteSpace(driveId) && gdrive.IsConnected)
    {
        try { await gdrive.DeleteDriveItemAsync(driveId, ctx.RequestAborted); } catch { }
    }
    await Helpers.Audit(db, Helpers.UserId(ctx.User), "story.text_delete", row.S("story_id"),
        $"textId={textId}; file={row.S("file_name")}");
    return Results.Ok(new { message = "Đã xóa file chữ thành công." });
});

// Track long-running 30+ minute MP3 transcoding & Cloudflare R2 uploads so retries
// do not spawn duplicate jobs and the client can verify Cloudflare R2 status before reporting.
var activeAudioUploads = new ConcurrentDictionary<string, (string Stage, DateTimeOffset StartedAt)>(StringComparer.Ordinal);
var recentAudioOutcomes = new ConcurrentDictionary<string, (bool Success, string Message, string EpisodeId, DateTimeOffset UpdatedAt)>(StringComparer.Ordinal);

admin.MapGet("/stories/{id}/episodes/upload-status", async Task<IResult> (
    string id, int? episodeNumber, string? episodeId, HttpContext ctx, Database source, AudioStorage storage) =>
{
    var jobKey = !string.IsNullOrWhiteSpace(episodeId) ? $"ep:{episodeId}" : $"story:{id}:num:{episodeNumber ?? 0}";
    if (activeAudioUploads.TryGetValue(jobKey, out var active))
    {
        return Results.Ok(new
        {
            inProgress = true,
            exists = false,
            r2Verified = false,
            stage = active.Stage,
            elapsedSeconds = (int)Math.Max(0, (DateTimeOffset.UtcNow - active.StartedAt).TotalSeconds)
        });
    }

    await using var db = await source.OpenAsync();
    var ep = !string.IsNullOrWhiteSpace(episodeId)
        ? await db.FirstAsync(@"SELECT id,title,episode_number,duration_seconds,full_file,preview_file,hls_ref,created_at
            FROM episodes WHERE id=@eid AND story_id=@sid", ("@eid", episodeId), ("@sid", id))
        : (episodeNumber is > 0
            ? await db.FirstAsync(@"SELECT id,title,episode_number,duration_seconds,full_file,preview_file,hls_ref,created_at
                FROM episodes WHERE story_id=@sid AND episode_number=@num", ("@sid", id), ("@num", episodeNumber.Value))
            : null);

    if (ep is not null)
    {
        var (ready, storageMode, detail) = await storage.VerifyEpisodeStorageAsync(
            ep.S("full_file"), ep.S("preview_file"), ep.S("hls_ref"), ctx.RequestAborted);
        return Results.Ok(new
        {
            inProgress = false,
            exists = true,
            r2Verified = ready,
            storage = storageMode,
            detail,
            id = ep.S("id"),
            episodeNumber = ep.N("episode_number"),
            title = ep.S("title"),
            durationSeconds = ep.N("duration_seconds"),
            hlsReady = ep.S("hls_ref") != ""
        });
    }

    if (recentAudioOutcomes.TryGetValue(jobKey, out var outcome) &&
        DateTimeOffset.UtcNow - outcome.UpdatedAt < TimeSpan.FromMinutes(10))
    {
        return Results.Ok(new
        {
            inProgress = false,
            exists = false,
            r2Verified = false,
            lastSuccess = outcome.Success,
            lastError = outcome.Success ? "" : outcome.Message
        });
    }

    return Results.Ok(new { inProgress = false, exists = false, r2Verified = false, lastError = "" });
});

admin.MapPost("/stories/{id}/episodes", async Task<IResult> (string id, HttpRequest request, HttpContext ctx, Database source, AudioStorage storage) =>
{
    if (!request.HasFormContentType) return Bad("Yêu cầu multipart/form-data.");
    var form = await request.ReadFormAsync(ctx.RequestAborted);
    var title = form["title"].ToString().Trim();
    if (!int.TryParse(form["episodeNumber"],out var number) || number < 1 || number > 100000 || title.Length is < 1 or > 180)
        return Bad("Tên tập hoặc số thứ tự không hợp lệ.");
    var uploaded = form.Files.GetFile("file");
    if (uploaded is null || uploaded.Length < 128 || uploaded.Length > 250L*1024*1024 ||
       Path.GetExtension(uploaded.FileName).ToLowerInvariant() != ".mp3") return Bad("Chỉ nhận MP3 hợp lệ tối đa 250MB.");
    await using var db = await source.OpenAsync();
    var story = await db.FirstAsync("SELECT storage_prefix FROM stories WHERE id=@id",("@id",id));
    if (story is null) return Results.NotFound();

    var jobKey = $"story:{id}:num:{number}";
    var startedAt = DateTimeOffset.UtcNow;
    if (!activeAudioUploads.TryAdd(jobKey, ("Đang nhận file MP3...", startedAt)))
        return Results.Conflict(new { error = "Tập này đang được máy chủ xử lý và tải lên Cloudflare R2. Vui lòng chờ hệ thống kiểm tra trạng thái.", inProgress = true });

    var existingEpId = Convert.ToString(await db.ValueAsync(
        "SELECT id FROM episodes WHERE story_id=@sid AND episode_number=@num LIMIT 1", ("@sid", id), ("@num", number))) ?? "";
    if (existingEpId != "")
    {
        activeAudioUploads.TryRemove(jobKey, out _);
        return Results.Conflict(new { error = "Số tập đã tồn tại trong bộ truyện.", alreadyExists = true, id = existingEpId });
    }

    var fileId = Helpers.Id();
    var tempRoot = Path.Combine(source.DataRoot, "upload-temp");
    Directory.CreateDirectory(tempRoot);
    var fullPath = Path.Combine(tempRoot,fileId+"-incoming.mp3");
    var previewPath = Path.Combine(tempRoot,fileId+"-preview.mp3");
    string storedFull="", storedPreview="", storedHls="";
    var hlsFolder=Path.Combine(tempRoot,fileId+"-hls");
    bool episodeSavedInDatabase = false;
    try
    {
        await using (var output = new FileStream(fullPath,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
            await uploaded.CopyToAsync(output,ctx.RequestAborted);
        var sig = new byte[3];
        await using (var input = File.OpenRead(fullPath)) await input.ReadExactlyAsync(sig,ctx.RequestAborted);
        if (!(sig[0]==73 && sig[1]==68 && sig[2]==51) && !(sig[0]==255 && (sig[1]&0xE0)==0xE0))
            throw new InvalidDataException("Không nhận diện được định dạng MP3.");

        // Once the MP3 bytes are on disk, do not abort FFmpeg/R2 if a proxy times out the HTTP response at 60-100s.
        using var procCts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        activeAudioUploads[jobKey] = ("Đang tạo bản nghe thử 5 phút (FFmpeg)...", startedAt);
        var ffmpeg = Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "ffmpeg";
        await RunProgram(ffmpeg,["-hide_banner","-loglevel","error","-y","-i",fullPath,"-t","300","-vn","-acodec","libmp3lame","-b:a","96k",previewPath]);
        if (!File.Exists(previewPath) || new FileInfo(previewPath).Length == 0) throw new InvalidDataException("FFmpeg không tạo được preview.");
        var duration = await ProbeDuration(fullPath);
        var epId = Helpers.Id();

        activeAudioUploads[jobKey] = ("Đang mã hóa HLS AES-128 cho tập dài...", startedAt);
        await HlsTranscoder.CreateAsync(fullPath,hlsFolder,procCts.Token);

        activeAudioUploads[jobKey] = ("Đang tải MP3 và HLS lên Cloudflare R2...", startedAt);
        (storedFull, storedPreview) = await storage.SaveEpisodeAsync(fullPath,previewPath,story.S("storage_prefix"),epId,number,procCts.Token);
        storedHls=await storage.SaveHlsAsync(hlsFolder,story.S("storage_prefix"),number,procCts.Token);

        activeAudioUploads[jobKey] = ("Đang xác nhận trạng thái file trên Cloudflare R2...", startedAt);
        var (r2Ready, storageMode, verifyDetail) = await storage.VerifyEpisodeStorageAsync(storedFull, storedPreview, storedHls, procCts.Token);
        if (!r2Ready) throw new InvalidDataException("Kiểm tra Cloudflare R2 chưa đạt: " + verifyDetail);

        await db.ExecAsync(@"INSERT INTO episodes(id,story_id,title,episode_number,full_file,preview_file,hls_ref,duration_seconds,created_at)
           VALUES(@id,@sid,@title,@number,@full,@preview,@hls,@duration,@now)",
           ("@id",epId),("@sid",id),("@title",title),("@number",number),("@full",storedFull),
           ("@preview",storedPreview),("@hls",storedHls),("@duration",duration),("@now",Helpers.Now()));
        episodeSavedInDatabase = true;
        recentAudioOutcomes[jobKey] = (true, verifyDetail, epId, DateTimeOffset.UtcNow);
        await Helpers.Audit(db,Helpers.UserId(ctx.User),"episode.upload",epId,$"story={id}; episode={number}; storage={storageMode}; r2Verified={r2Ready}");
        return Results.Ok(new { id = epId, durationSeconds = duration, previewSeconds = Math.Min(duration,300), storage = storageMode, r2Verified = r2Ready, detail = verifyDetail });
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
    {
        if (!episodeSavedInDatabase) await storage.DeleteBestEffortAsync(storedFull,storedPreview,storedHls);
        recentAudioOutcomes[jobKey] = (false, "Số tập đã tồn tại trong bộ truyện.", "", DateTimeOffset.UtcNow);
        return Results.Conflict(new { error = "Số tập đã tồn tại trong bộ truyện.", alreadyExists = true });
    }
    catch (Exception ex)
    {
        if (!episodeSavedInDatabase) await storage.DeleteBestEffortAsync(storedFull,storedPreview,storedHls);
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AudioUpload").LogError(ex,"Không thể upload Audio/R2");
        var msg = ex is InvalidDataException ? ex.Message : "Upload thất bại. Kiểm tra FFmpeg, Cloudflare R2 và log máy chủ.";
        recentAudioOutcomes[jobKey] = (false, msg, "", DateTimeOffset.UtcNow);
        return Bad(msg);
    }
    finally
    {
        activeAudioUploads.TryRemove(jobKey, out _);
        DeleteFiles(fullPath,previewPath);
        if(Directory.Exists(hlsFolder)) Directory.Delete(hlsFolder,true);
    }
}).DisableAntiforgery();

// Optional move/copy for old Local OR legacy R2 audio. Old objects are retained as backup.
admin.MapPost("/episodes/{id}/migrate-r2", async Task<IResult> (string id, HttpContext ctx, Database source, AudioStorage storage) =>
{
    if (!storage.IsR2) return Bad("Phải bật LKH_AUDIO_STORAGE=R2 trước khi di chuyển.");
    await using var db = await source.OpenAsync();
    var ep = await db.FirstAsync(@"SELECT e.full_file,e.preview_file,e.episode_number,s.storage_prefix
        FROM episodes e JOIN stories s ON s.id=e.story_id WHERE e.id=@id",("@id",id));
    if (ep is null) return Results.NotFound();
    var oldFull=ep.S("full_file"); var oldPreview=ep.S("preview_file");
    if (oldFull.StartsWith("r2:stories/",StringComparison.Ordinal))
        return Bad("Tập này đã được lưu theo thư mục bộ truyện.");
    string newFull="", newPreview="";
    bool committed=false;
    try
    {
        (newFull,newPreview) = await storage.OrganizeEpisodeAsync(oldFull,oldPreview,
            ep.S("storage_prefix"),id,ep.N("episode_number"),ctx.RequestAborted);
        var changed=await db.ExecAsync(@"UPDATE episodes SET full_file=@newFull,preview_file=@newPreview
             WHERE id=@id AND full_file=@oldFull AND preview_file=@oldPreview",
             ("@newFull",newFull),("@newPreview",newPreview),("@id",id),
             ("@oldFull",oldFull),("@oldPreview",oldPreview));
        if (changed != 1) { await storage.DeleteBestEffortAsync(newFull,newPreview); return Results.Conflict(new{error="Tập đã thay đổi khi chuyển file."}); }
        committed=true;
        await Helpers.Audit(db,Helpers.UserId(ctx.User),"episode.organize_r2",id,
           "Moved reference to per-story folder. Old Local/R2 copies retained.");
        return Results.Ok(new {message="Đã đưa Full/Preview vào thư mục truyện trên R2. Bản cũ được giữ lại."});
    }
    catch(Exception ex)
    {
        if (!committed && newFull!="") await storage.DeleteBestEffortAsync(newFull,newPreview);
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AudioMigration").LogError(ex,"Lỗi tổ chức audio R2");
        return Bad("Di chuyển thất bại. Kiểm tra Cloudflare R2 và log hệ thống.");
    }
});
// Replace only the binary file of an existing episode. Title/order/status stay intact.
// The preview is regenerated from the new MP3 and both objects are swapped together.
admin.MapPut("/episodes/{id}/audio", async Task<IResult> (string id, HttpRequest request, HttpContext ctx,
    Database source, AudioStorage storage, StorageJanitor janitor) =>
{
    if (!request.HasFormContentType) return Bad("Yêu cầu multipart/form-data.");
    var form = await request.ReadFormAsync(ctx.RequestAborted);
    var uploaded = form.Files.GetFile("file");
    if (uploaded is null || uploaded.Length < 128 || uploaded.Length > 250L*1024*1024 ||
        Path.GetExtension(uploaded.FileName).ToLowerInvariant() != ".mp3")
        return Bad("Chỉ nhận file MP3 hợp lệ tối đa 250MB.");

    await using var db = await source.OpenAsync();
    var old = await db.FirstAsync(@"SELECT e.full_file,e.preview_file,e.hls_ref,e.episode_number,s.storage_prefix
        FROM episodes e JOIN stories s ON s.id=e.story_id WHERE e.id=@id",("@id",id));
    if (old is null) return Results.NotFound();

    var jobKey = $"ep:{id}";
    var startedAt = DateTimeOffset.UtcNow;
    if (!activeAudioUploads.TryAdd(jobKey, ("Đang nhận file MP3 thay thế...", startedAt)))
        return Results.Conflict(new { error = "Tập này đang được máy chủ thay thế audio và tải lên Cloudflare R2. Vui lòng đợi.", inProgress = true });

    var tempRoot = Path.Combine(source.DataRoot,"upload-temp");
    Directory.CreateDirectory(tempRoot);
    var key = Helpers.Id();
    var fullPath = Path.Combine(tempRoot,key+"-incoming.mp3");
    var previewPath = Path.Combine(tempRoot,key+"-preview.mp3");
    string newFull="",newPreview="",newHls="";
    var hlsFolder=Path.Combine(tempRoot,key+"-hls");
    bool committed=false;
    try
    {
        await using (var output = new FileStream(fullPath,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
            await uploaded.CopyToAsync(output,ctx.RequestAborted);
        var sig = new byte[3];
        await using (var input = File.OpenRead(fullPath)) await input.ReadExactlyAsync(sig,ctx.RequestAborted);
        if (!(sig[0]==73 && sig[1]==68 && sig[2]==51) &&
            !(sig[0]==255 && (sig[1]&0xE0)==0xE0))
            throw new InvalidDataException("Không nhận diện được file MP3.");

        using var procCts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        activeAudioUploads[jobKey] = ("Đang tạo bản nghe thử 5 phút (FFmpeg)...", startedAt);
        var ffmpeg = Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "ffmpeg";
        await RunProgram(ffmpeg,["-hide_banner","-loglevel","error","-y","-i",fullPath,
            "-t","300","-vn","-acodec","libmp3lame","-b:a","96k",previewPath]);
        if (!File.Exists(previewPath) || new FileInfo(previewPath).Length == 0)
            throw new InvalidDataException("Không tạo được bản nghe thử.");
        var duration = await ProbeDuration(fullPath);

        activeAudioUploads[jobKey] = ("Đang mã hóa HLS AES-128...", startedAt);
        await HlsTranscoder.CreateAsync(fullPath,hlsFolder,procCts.Token);

        activeAudioUploads[jobKey] = ("Đang tải MP3 và HLS lên Cloudflare R2...", startedAt);
        (newFull,newPreview) = await storage.SaveEpisodeAsync(fullPath,previewPath,
            old.S("storage_prefix"),id,old.N("episode_number"),procCts.Token);
        newHls=await storage.SaveHlsAsync(hlsFolder,old.S("storage_prefix"),old.N("episode_number"),procCts.Token);

        activeAudioUploads[jobKey] = ("Đang xác nhận trạng thái file trên Cloudflare R2...", startedAt);
        var (r2Ready, storageMode, verifyDetail) = await storage.VerifyEpisodeStorageAsync(newFull, newPreview, newHls, procCts.Token);
        if (!r2Ready) throw new InvalidDataException("Kiểm tra Cloudflare R2 chưa đạt: " + verifyDetail);

        await using (var tx=(SqliteTransaction)await db.BeginTransactionAsync())
        {
            await using var cmd=DbExtensions.Cmd(db,@"UPDATE episodes
                SET full_file=@full,preview_file=@preview,hls_ref=@hls,duration_seconds=@duration
                WHERE id=@id AND full_file=@oldFull AND preview_file=@oldPreview AND hls_ref=@oldHls",tx,
                ("@full",newFull),("@preview",newPreview),("@hls",newHls),("@duration",duration),("@id",id),
                ("@oldFull",old.S("full_file")),("@oldPreview",old.S("preview_file")),("@oldHls",old.S("hls_ref")));
            if (await cmd.ExecuteNonQueryAsync() != 1)
            {
                await tx.RollbackAsync();
                await storage.DeleteBestEffortAsync(newFull,newPreview,newHls);
                return Results.Conflict(new { error="Tập đã bị thay đổi hoặc xóa. Hãy tải lại danh sách." });
            }
            await StorageJanitor.QueueAsync(db,tx,old.S("full_file"),old.S("preview_file"),old.S("hls_ref"));
            await Helpers.Audit(db,Helpers.UserId(ctx.User),"episode.audio_replace",id,
                $"duration={duration}; storage={storageMode}; r2Verified={r2Ready}; old queued",tx);
            await tx.CommitAsync();
        }
        committed=true;
        recentAudioOutcomes[jobKey] = (true, verifyDetail, id, DateTimeOffset.UtcNow);
        await janitor.TrySweepAsync();
        return Results.Ok(new { message="Đã thay MP3, xác nhận trên Cloudflare R2 và tạo lại preview. File cũ được đưa vào hàng đợi dọn dẹp.",durationSeconds=duration,storage=storageMode,r2Verified=r2Ready });
    }
    catch(Exception ex)
    {
        if (!committed) await storage.DeleteBestEffortAsync(newFull,newPreview,newHls);
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AudioReplace")
            .LogError(ex,"Thay MP3 thất bại");
        var msg = ex is InvalidDataException ? ex.Message : "Không thay được MP3. Kiểm tra FFmpeg, R2 và log máy chủ.";
        recentAudioOutcomes[jobKey] = (false, msg, id, DateTimeOffset.UtcNow);
        return Bad(msg);
    }
    finally
    {
        activeAudioUploads.TryRemove(jobKey, out _);
        DeleteFiles(fullPath,previewPath);
        if(Directory.Exists(hlsFolder)) Directory.Delete(hlsFolder,true);
    }
}).DisableAntiforgery();

// Deleting a published episode also removes saved listening progress; keep a durable
// deletion outbox for Full and Preview so R2 failures can be retried safely.
admin.MapDelete("/episodes/{id}", async Task<IResult> (string id, HttpContext ctx,
    Database source, StorageJanitor janitor) =>
{
    await using var db = await source.OpenAsync();
    await using var tx=(SqliteTransaction)await db.BeginTransactionAsync();
    Dictionary<string,object?>? ep;
    await using (var get=DbExtensions.Cmd(db,
        "SELECT full_file,preview_file,story_id,episode_number,hls_ref FROM episodes WHERE id=@id",tx,("@id",id)))
    await using (var reader=await get.ExecuteReaderAsync())
    {
        if (!await reader.ReadAsync()) return Results.NotFound();
        ep=new Dictionary<string,object?>
        {
            ["full_file"]=reader.GetString(0),["preview_file"]=reader.GetString(1),
            ["story_id"]=reader.GetString(2),["episode_number"]=reader.GetInt32(3),["hls_ref"]=reader.GetString(4)
        };
    }
    await using (var progress=DbExtensions.Cmd(db,"DELETE FROM listening_progress WHERE episode_id=@id",tx,("@id",id)))
        await progress.ExecuteNonQueryAsync();
    await using (var delete=DbExtensions.Cmd(db,@"DELETE FROM episodes
        WHERE id=@id AND full_file=@full AND preview_file=@preview",tx,
        ("@id",id),("@full",ep.S("full_file")),("@preview",ep.S("preview_file"))))
    {
        if (await delete.ExecuteNonQueryAsync()!=1)
        {
            await tx.RollbackAsync();
            return Results.Conflict(new { error="Tập đã thay đổi. Hãy tải lại trước khi xóa." });
        }
    }
    await StorageJanitor.QueueAsync(db,tx,ep.S("full_file"),ep.S("preview_file"),ep.S("hls_ref"));
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"episode.delete",id,
        $"story={ep.S("story_id")}; number={ep.N("episode_number")}; media queued",tx);
    await tx.CommitAsync();
    await janitor.TrySweepAsync();
    return Results.Ok(new { message="Đã xóa tập, lịch sử nghe và yêu cầu xóa file R2/Local cũ." });
});

// Upgrade old episodes to Premium HLS once without re-uploading their source MP3.
admin.MapPost("/episodes/{id}/prepare-hls", async Task<IResult> (string id, HttpContext ctx,
    Database source, AudioStorage storage, StorageJanitor janitor) =>
{
    await using var db=await source.OpenAsync();
    var ep=await db.FirstAsync(@"SELECT e.full_file,e.hls_ref,e.episode_number,s.storage_prefix
        FROM episodes e JOIN stories s ON e.story_id=s.id WHERE e.id=@id",("@id",id));
    if(ep is null) return Results.NotFound();
    var root=Path.Combine(source.DataRoot,"upload-temp","hls-"+Helpers.Id());
    Directory.CreateDirectory(root);
    var mp3=Path.Combine(root,"source.mp3");
    var output=Path.Combine(root,"hls");
    string newHls=""; bool committed=false;
    try
    {
        await storage.MaterializeSourceAudioAsync(ep.S("full_file"),mp3,ctx.RequestAborted);
        await HlsTranscoder.CreateAsync(mp3,output,ctx.RequestAborted);
        newHls=await storage.SaveHlsAsync(output,ep.S("storage_prefix"),ep.N("episode_number"),ctx.RequestAborted);
        await using(var tx=(SqliteTransaction)await db.BeginTransactionAsync())
        {
            await using var cmd=DbExtensions.Cmd(db,"UPDATE episodes SET hls_ref=@n WHERE id=@id AND hls_ref=@old",tx,
                ("@n",newHls),("@id",id),("@old",ep.S("hls_ref")));
            if(await cmd.ExecuteNonQueryAsync()!=1)
            { await tx.RollbackAsync(); return Results.Conflict(new {error="Tập vừa được cập nhật ở phiên khác."}); }
            await StorageJanitor.QueueAsync(db,tx,ep.S("hls_ref"));
            await Helpers.Audit(db,Helpers.UserId(ctx.User),"episode.hls_prepare",id,"Created encrypted HLS for Premium",tx);
            await tx.CommitAsync();
        }
        committed=true;
        await janitor.TrySweepAsync();
        return Results.Ok(new{message="Đã tạo HLS Premium từ MP3 gốc."});
    }
    catch(Exception ex)
    {
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("HlsMigration")
            .LogError(ex,"Lỗi tạo HLS cho tập {Id}",id);
        return Bad("Không thể tạo HLS. Kiểm tra FFmpeg, quyền R2 và log.");
    }
    finally
    {
        if(!committed && newHls!="") await storage.DeleteBestEffortAsync(newHls);
        try { if(Directory.Exists(root)) Directory.Delete(root,true); } catch { }
    }
});

admin.MapPut("/episodes/{id}", async Task<IResult> (string id, EpisodeReq req, HttpContext ctx, Database source) =>
{
    if (req.Title.Trim().Length is < 1 or > 180 || req.EpisodeNumber < 1 || req.EpisodeNumber > 100000 ||
        !new[] { "Published","Hidden" }.Contains(req.Status)) return Bad("Dữ liệu tập không hợp lệ.");
    await using var db = await source.OpenAsync();
    try
    {
        var n = await db.ExecAsync("UPDATE episodes SET title=@t,episode_number=@n,status=@s WHERE id=@id",("@t",req.Title.Trim()),("@n",req.EpisodeNumber),("@s",req.Status),("@id",id));
        if (n == 0) return Results.NotFound();
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { return Results.Conflict(new { error = "Số thứ tự tập bị trùng." }); }
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"episode.update",id,req.Title);
    return Results.Ok(new { message = "Đã cập nhật tập." });
});
admin.MapGet("/plans", async Task<IResult> (Database source) =>
{
    await using var db = await source.OpenAsync();
    return Results.Ok(await db.ListAsync("SELECT * FROM subscription_plans"));
});
admin.MapPut("/plans/premium", async Task<IResult> (PriceReq req, HttpContext ctx, Database source) =>
{
    if (req.PriceVnd < 0 || req.PriceVnd > 100000000) return Bad("Giá không hợp lệ.");
    await using var db = await source.OpenAsync();
    await db.ExecAsync("UPDATE subscription_plans SET price_vnd=@p,updated_at=@now WHERE id='premium'",("@p",req.PriceVnd),("@now",Helpers.Now()));
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"plan.price", "premium",$"new_price_vnd={req.PriceVnd}");
    return Results.Ok(new { message = "Đã thay đổi giá. Đơn cũ giữ nguyên giá đã tạo." });
});
// The public banking catalog is proxied server-side and cached; client needs no VietQR credentials.
admin.MapGet("/banks/catalog", async Task<IResult> (IHttpClientFactory clientFactory, IMemoryCache cache) =>
{
    if(cache.TryGetValue("vietqr-bank-catalog-v1",out object? stored) && stored is not null)
        return Results.Ok(new { data = stored });
    try
    {
        using var client=clientFactory.CreateClient("VietQrBanks");
        using var response=await client.GetAsync("https://api.vietqr.io/v2/banks");
        response.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if(!json.RootElement.TryGetProperty("code",out var code) || code.GetString()!="00" ||
           !json.RootElement.TryGetProperty("data",out var data) || data.ValueKind!=JsonValueKind.Array)
            return Results.Problem("Không thể đọc danh sách ngân hàng VietQR.",statusCode:502);
        var banks=data.EnumerateArray()
            .Where(x=>x.TryGetProperty("bin",out var bin) && bin.ValueKind==JsonValueKind.String)
            .Select(x=>new {
                bin=x.GetProperty("bin").GetString() ?? "",
                name=x.TryGetProperty("shortName",out var shortName) ? shortName.GetString() ?? "" : "",
                fullName=x.TryGetProperty("name",out var bankName) ? bankName.GetString() ?? "" : ""
            })
            .Where(x=>Regex.IsMatch(x.bin,@"^\d{6,8}$"))
            .OrderBy(x=>x.name).ToArray();
        cache.Set("vietqr-bank-catalog-v1",banks,TimeSpan.FromHours(12));
        return Results.Ok(new { data=banks });
    }
    catch(Exception){return Results.Json(new { error="Không tải được danh sách ngân hàng. Bạn có thể nhập BIN và tên thủ công." },statusCode:503);}
});
admin.MapGet("/bank-accounts", async Task<IResult> (Database source) =>
{
    await using var db=await source.OpenAsync();
    return Results.Ok(await db.ListAsync(@"SELECT id,label,bank_bin,bank_name,account_number,account_name,is_active,is_default,created_at,updated_at
        FROM bank_receivers ORDER BY is_default DESC,is_active DESC,created_at DESC"));
});
admin.MapPost("/bank-accounts", async Task<IResult> (BankAccountReq req, HttpContext ctx, Database source) =>
{
    var error=CheckBankAccount(req);
    if(error is not null) return Bad(error);
    await using var db=await source.OpenAsync();
    await using var tx=(SqliteTransaction)await db.BeginTransactionAsync();
    var id=Helpers.Id(); var now=Helpers.Now();
    try
    {
        var existing=await ValueTx(db,tx,"SELECT COUNT(*) FROM bank_receivers WHERE is_default=1");
        var first=Convert.ToInt32(existing)==0;
        await ExecTx(db,tx,@"INSERT INTO bank_receivers(id,label,bank_bin,bank_name,account_number,account_name,is_active,is_default,created_at,updated_at)
            VALUES(@id,@label,@bin,@name,@account,@owner,1,@default,@now,@now)",
            ("@id",id),("@label",string.IsNullOrWhiteSpace(req.Label)?req.BankName.Trim():req.Label.Trim()),("@bin",req.BankBin.Trim()),("@name",req.BankName.Trim()),
            ("@account",req.AccountNumber.Trim()),("@owner",req.AccountName.Trim()),("@default",first?1:0),("@now",now));
        await Helpers.Audit(db,Helpers.UserId(ctx.User),"bank.create",id,$"Bank BIN={req.BankBin}; account=***{Last4(req.AccountNumber)}",tx);
        await tx.CommitAsync();
        return Results.Ok(new { id,message=first?"Đã thêm và đặt mặc định.":"Đã thêm tài khoản nhận tiền." });
    }
    catch(SqliteException ex) when(ex.SqliteErrorCode==19){return Results.Conflict(new {error="Tài khoản ngân hàng đã tồn tại."});}
});
admin.MapPut("/bank-accounts/{id}", async Task<IResult> (string id, BankAccountReq req, HttpContext ctx, Database source) =>
{
    var error=CheckBankAccount(req);
    if(error is not null) return Bad(error);
    await using var db=await source.OpenAsync();
    try
    {
        var n=await db.ExecAsync(@"UPDATE bank_receivers SET label=@label,bank_bin=@bin,bank_name=@name,
            account_number=@account,account_name=@owner,updated_at=@now WHERE id=@id",
            ("@label",string.IsNullOrWhiteSpace(req.Label)?req.BankName.Trim():req.Label.Trim()),("@bin",req.BankBin.Trim()),("@name",req.BankName.Trim()),
            ("@account",req.AccountNumber.Trim()),("@owner",req.AccountName.Trim()),("@now",Helpers.Now()),("@id",id));
        if(n==0)return Results.NotFound();
        await Helpers.Audit(db,Helpers.UserId(ctx.User),"bank.update",id,$"Bank BIN={req.BankBin}; account=***{Last4(req.AccountNumber)}");
        return Results.Ok(new {message="Đã cập nhật tài khoản. Đơn hàng cũ giữ nguyên thông tin nhận tiền."});
    }
    catch(SqliteException ex) when(ex.SqliteErrorCode==19){return Results.Conflict(new{error="Số tài khoản này đã được thêm."});}
});
admin.MapPost("/bank-accounts/{id}/default", async Task<IResult> (string id, HttpContext ctx, Database source) =>
{
    await using var db=await source.OpenAsync();
    await using var tx=(SqliteTransaction)await db.BeginTransactionAsync();
    var exists=await ValueTx(db,tx,"SELECT 1 FROM bank_receivers WHERE id=@id AND is_active=1",("@id",id));
    if(exists is null)return Bad("Chỉ có thể đặt mặc định cho tài khoản đang hoạt động.");
    await ExecTx(db,tx,"UPDATE bank_receivers SET is_default=0 WHERE is_default=1");
    await ExecTx(db,tx,"UPDATE bank_receivers SET is_default=1,updated_at=@now WHERE id=@id",("@id",id),("@now",Helpers.Now()));
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"bank.default",id,"Đổi tài khoản thụ hưởng mặc định",tx);
    await tx.CommitAsync();
    return Results.Ok(new{message="Đã đổi tài khoản nhận tiền mặc định cho các đơn mới."});
});
admin.MapPost("/bank-accounts/{id}/activation", async Task<IResult> (string id, BankActivationReq req, HttpContext ctx, Database source) =>
{
    await using var db=await source.OpenAsync();
    await using var tx=(SqliteTransaction)await db.BeginTransactionAsync();
    var bank=await RowTx(db,tx,"SELECT is_active,is_default FROM bank_receivers WHERE id=@id",("@id",id));
    if(bank is null) return Results.NotFound();
    if(!req.IsActive && bank.N("is_default")==1)
        return Bad("Hãy chọn một tài khoản mặc định khác trước khi ngừng sử dụng tài khoản này.");
    await ExecTx(db,tx,"UPDATE bank_receivers SET is_active=@active,updated_at=@now WHERE id=@id",
        ("@active",req.IsActive?1:0),("@now",Helpers.Now()),("@id",id));
    await Helpers.Audit(db,Helpers.UserId(ctx.User),req.IsActive?"bank.enable":"bank.disable",id,"Changed receiving bank availability",tx);
    await tx.CommitAsync();
    return Results.Ok(new{message=req.IsActive?"Đã kích hoạt tài khoản.":"Đã ngừng sử dụng tài khoản."});
});
// Legacy endpoint stays usable for older Admin pages, but now accesses the active default account.
admin.MapGet("/bank", async Task<IResult> (Database source) =>
{
    await using var db=await source.OpenAsync();
    return Results.Ok(await db.FirstAsync("SELECT * FROM bank_receivers WHERE is_default=1 AND is_active=1"));
});
admin.MapPut("/bank", async Task<IResult> (BankReq req, HttpContext ctx, Database source) =>
{
    var validated=new BankAccountReq("Tài khoản nhận tiền",req.BankBin,req.BankName,req.AccountNumber,req.AccountName);
    var error=CheckBankAccount(validated);
    if(error is not null)return Bad(error);
    await using var db=await source.OpenAsync();
    var bank=await db.FirstAsync("SELECT id FROM bank_receivers WHERE is_default=1 AND is_active=1");
    if(bank is null) return Results.Conflict(new{error="Chưa có tài khoản mặc định. Hãy tạo tại mục Tài khoản QR."});
    try
    {
        await db.ExecAsync("UPDATE bank_receivers SET bank_bin=@bin,bank_name=@name,account_number=@account,account_name=@owner,updated_at=@now WHERE id=@id",
            ("@bin",req.BankBin.Trim()),("@name",req.BankName.Trim()),("@account",req.AccountNumber.Trim()),
            ("@owner",req.AccountName.Trim()),("@now",Helpers.Now()),("@id",bank.S("id")));
        await Helpers.Audit(db,Helpers.UserId(ctx.User),"bank.update",bank.S("id"),"Legacy update of current default (account redacted)");
        return Results.Ok(new{message="Đã cập nhật tài khoản mặc định."});
    }
    catch(SqliteException ex) when(ex.SqliteErrorCode==19){return Results.Conflict(new{error="Tài khoản trùng."});}
});

admin.MapGet("/orders", async Task<IResult> (Database source, string? status) =>
{
    await using var db = await source.OpenAsync();
    await ExpireOrders(db);
    return Results.Ok(await db.ListAsync(@"SELECT o.id,o.code,o.amount_vnd,o.duration_months,o.status,o.order_type,o.grant_reason,
       o.premium_start_at,o.premium_end_at,o.created_at,o.expires_at,o.paid_at,o.review_note,
       o.bank_name,o.account_number,o.account_name,o.bank_bin,o.bank_receiver_id,
       u.username,u.full_name,COALESCE(admin.username,'') AS granted_by_name
       FROM orders o JOIN users u ON u.id=o.user_id LEFT JOIN users admin ON admin.id=o.reviewed_by
       WHERE (@status='' OR o.status=@status) ORDER BY o.created_at DESC LIMIT 200",("@status",status??"")));
});
admin.MapPost("/orders/{id}/confirm", async Task<IResult> (string id, ConfirmReq req, HttpContext ctx, Database source) =>
{
    if (req.BankReference.Trim().Length is < 5 or > 120) return Bad("Phải nhập mã giao dịch ngân hàng thực tế.");
    await using var db = await source.OpenAsync();
    try
    {
        await using var tx = (SqliteTransaction)await db.BeginTransactionAsync();
        var order = await RowTx(db,tx,"SELECT * FROM orders WHERE id=@id",("@id",id));
        if (order is null) return Results.NotFound();
        if (order.S("order_type") != "Purchase") return Bad("Đơn do Admin cấp không phải giao dịch ngân hàng.");
        if (order.S("status") is not ("Pending" or "Reviewing")) return Results.Conflict(new { error = "Đơn đã xử lý hoặc quá hạn." });
        if (DateTimeOffset.Parse(order.S("expires_at")) <= DateTimeOffset.UtcNow) return Bad("Đơn quá hạn, không xác nhận tự động. Hãy xử lý đối soát riêng.");
        var n = await ExecTx(db,tx,"UPDATE orders SET status='Paid',paid_at=@now,reviewed_by=@admin WHERE id=@id AND status IN ('Pending','Reviewing')",
            ("@now",Helpers.Now()),("@admin",Helpers.UserId(ctx.User)),("@id",id));
        if (n != 1) return Results.Conflict(new { error = "Đơn đã được xử lý." });
        await ExecTx(db,tx,"INSERT INTO payments(id,order_id,bank_reference,amount_vnd,confirmed_by,confirmed_at) VALUES(@pid,@id,@ref,@amount,@admin,@now)",
            ("@pid",Helpers.Id()),("@id",id),("@ref",req.BankReference.Trim()),("@amount",order.N("amount_vnd")),
            ("@admin",Helpers.UserId(ctx.User)),("@now",Helpers.Now()));
        var until = await PremiumUntilTx(db,tx,order.S("user_id"));
        var start = Helpers.IsPremium(until) ? DateTimeOffset.Parse(until) : DateTimeOffset.UtcNow;
        var end = start.AddMonths(order.N("duration_months"));
        await ExecTx(db,tx,@"INSERT INTO subscriptions(id,user_id,starts_at,ends_at,source,order_id,granted_by,note,created_at)
            VALUES(@sid,@uid,@start,@end,'Order',@order,@admin,'',@now)",
            ("@sid",Helpers.Id()),("@uid",order.S("user_id")),("@start",start.ToString("O")),("@end",end.ToString("O")),
            ("@order",id),("@admin",Helpers.UserId(ctx.User)),("@now",Helpers.Now()));
        await ExecTx(db,tx,"UPDATE orders SET premium_start_at=@start,premium_end_at=@end WHERE id=@id",
            ("@start",start.ToString("O")),("@end",end.ToString("O")),("@id",id));
        await Helpers.Audit(db,Helpers.UserId(ctx.User),"order.confirm",id,$"bank_reference={req.BankReference.Trim()}; amount={order.N("amount_vnd")}",tx);
        await tx.CommitAsync();
        return Results.Ok(new { message = "Đã duyệt thanh toán và gia hạn Premium.", premiumUntil = end.ToString("O") });
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { return Results.Conflict(new { error = "Mã giao dịch ngân hàng đã được dùng hoặc đơn đã thanh toán." }); }
});
admin.MapPost("/orders/{id}/reject", async Task<IResult> (string id, RejectReq req, HttpContext ctx, Database source) =>
{
    if (req.Reason.Trim().Length is < 3 or > 300) return Bad("Cần nhập lý do từ chối.");
    await using var db = await source.OpenAsync();
    var n = await db.ExecAsync("UPDATE orders SET status='Rejected',review_note=@note,reviewed_by=@admin WHERE id=@id AND order_type='Purchase' AND status IN ('Pending','Reviewing')",
       ("@note",req.Reason.Trim()),("@admin",Helpers.UserId(ctx.User)),("@id",id));
    if (n == 0) return Bad("Đơn không thể từ chối.");
    await Helpers.Audit(db,Helpers.UserId(ctx.User),"order.reject",id,req.Reason.Trim());
    return Results.Ok(new { message = "Đã từ chối đơn." });
});
admin.MapGet("/audit", async Task<IResult> (Database source) =>
{
    await using var db = await source.OpenAsync();
    return Results.Ok(await db.ListAsync("SELECT id,actor_id,action,subject_id,details,created_at FROM audit_logs ORDER BY created_at DESC LIMIT 200"));
});
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

static IResult Bad(string message) => Results.BadRequest(new { error = message });
static string? ValidateStory(StoryReq x)
{
    if(string.IsNullOrWhiteSpace(x.Title) || x.Title.Trim().Length>180 ||
       x.Author is null || x.Author.Length>120 || x.Description is null || x.Description.Length>15000)
        return "Thông tin truyện vượt giới hạn.";
    if(!new[] {"Draft","Published","Hidden"}.Contains(x.Status)) return "Trạng thái không hợp lệ.";
    if(!string.IsNullOrWhiteSpace(x.CoverUrl) && (!Uri.TryCreate(x.CoverUrl,UriKind.Absolute,out var uri)
       || uri.Scheme!=Uri.UriSchemeHttps || x.CoverUrl.Length>1000)) return "Ảnh bìa cần URL HTTPS hợp lệ.";
    return null;
}
static async Task<string> UniqueSlugAsync(SqliteConnection db,string name,string table)
{
    // Fixed table names only; the suffix avoids conflicts for identical Vietnamese titles.
    if(table is not ("stories" or "genres")) throw new ArgumentException("Invalid slug table");
    var baseSlug=Slugs.FromTitle(name);
    for(var i=1;i<10000;i++)
    {
        var suffix=i==1 ? "" : "-"+i;
        var stem=baseSlug.Length+suffix.Length>145 ? baseSlug[..(145-suffix.Length)].TrimEnd('-') : baseSlug;
        var slug=stem+suffix;
        if(await db.ValueAsync($"SELECT 1 FROM {table} WHERE slug=@slug LIMIT 1",("@slug",slug)) is null)
            return slug;
    }
    throw new InvalidOperationException("Không thể tạo slug duy nhất.");
}
static async Task<List<(string slug,string name)>?> ResolveGenresAsync(SqliteConnection db,StoryReq req,string? storyId=null)
{
    var slugs=(req.Genres ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
    if(slugs.Length==0 && !string.IsNullOrWhiteSpace(req.Genre))
    {
        var existing=await db.FirstAsync("SELECT slug FROM genres WHERE name=@name COLLATE NOCASE AND is_active=1",
          ("@name",req.Genre.Trim()));
        if(existing is not null) slugs=[existing.S("slug")];
    }
    if(slugs.Length==0 || slugs.Length>12)return null;
    var result=new List<(string slug,string name)>();
    foreach(var slug in slugs)
    {
        var row=await db.FirstAsync(@"SELECT slug,name FROM genres WHERE slug=@slug AND
            (is_active=1 OR EXISTS(SELECT 1 FROM story_genres WHERE genre_slug=@slug AND story_id=@story))",
            ("@slug",slug),("@story",storyId ?? ""));
        if(row is null)return null;
        result.Add((row.S("slug"),row.S("name")));
    }
    return result;
}
static string? CheckBankAccount(BankAccountReq req)
{
    if(req.BankBin is null || req.BankName is null || req.AccountNumber is null || req.AccountName is null || req.Label is null)
        return "Thông tin ngân hàng không được để trống.";
    if(!Regex.IsMatch(req.BankBin.Trim(),@"^\d{6,8}$") ||
       !Regex.IsMatch(req.AccountNumber.Trim(),@"^\d{5,25}$") ||
       req.BankName.Trim().Length is < 2 or > 100 ||
       req.AccountName.Trim().Length is < 3 or > 120 || req.Label.Trim().Length > 80)
        return "Kiểm tra mã BIN (6–8 số), số tài khoản (5–25 số), tên ngân hàng và tên người nhận.";
    return null;
}
static string Last4(string account) => account.Length<=4 ? account : account[^4..];
static async Task<object?> OrderDto(SqliteConnection db,string id)
{
    var order = await db.FirstAsync("SELECT * FROM orders WHERE id=@id",("@id",id));
    if (order is null) return null;
    var adminGrant = order.S("order_type") == "AdminGrant";
    var bin=order.S("bank_bin"); var acc=order.S("account_number");
    var query = $"amount={order.N("amount_vnd")}&addInfo={Uri.EscapeDataString(order.S("code"))}&accountName={Uri.EscapeDataString(order.S("account_name"))}";
    var qr = adminGrant || bin == "" || acc == "" ? "" :
       $"https://img.vietqr.io/image/{Uri.EscapeDataString(bin)}-{Uri.EscapeDataString(acc)}-compact2.svg?{query}";
    var period = await db.FirstAsync("SELECT starts_at,ends_at FROM subscriptions WHERE order_id=@id LIMIT 1",("@id",id));
    var grantedBy = adminGrant ? await db.ValueAsync("SELECT username FROM users WHERE id=@admin",("@admin",order.S("reviewed_by"))) : null;
    var premiumStart = order.S("premium_start_at");
    if (premiumStart == "") premiumStart = period?.S("starts_at") ?? "";
    var premiumEnd = order.S("premium_end_at");
    if (premiumEnd == "") premiumEnd = period?.S("ends_at") ?? "";
    return new { id=order.S("id"), code=order.S("code"), amountVnd=order.N("amount_vnd"), durationMonths=order.N("duration_months"),
      orderType=order.S("order_type"),grantReason=order.S("grant_reason"),grantedByName=Convert.ToString(grantedBy)??"",
      premiumStartAt=premiumStart,premiumEndAt=premiumEnd,
      bankName=order.S("bank_name"),bankBin=bin,accountNumber=acc,accountName=order.S("account_name"),
      status=order.S("status"),createdAt=order.S("created_at"),expiresAt=order.S("expires_at"),paidAt=order.S("paid_at"),qrUrl=qr };
}
static Task<int> ExpireOrders(SqliteConnection db) => db.ExecAsync("UPDATE orders SET status='Expired' WHERE order_type='Purchase' AND status='Pending' AND expires_at<=@now",("@now",Helpers.Now()));
static async Task<Dictionary<string,object?>?> RowTx(SqliteConnection db, SqliteTransaction tx,string sql,params (string,object?)[] args)
{
    await using var cmd=DbExtensions.Cmd(db,sql,tx,args);
    await using var r=await cmd.ExecuteReaderAsync();
    if (!await r.ReadAsync()) return null;
    var row=new Dictionary<string,object?>();
    for (var i=0;i<r.FieldCount;i++) row[r.GetName(i)]=r.IsDBNull(i)?null:r.GetValue(i);
    return row;
}
static async Task<object?> ValueTx(SqliteConnection db,SqliteTransaction tx,string sql,params (string,object?)[] args)
{
    await using var cmd=DbExtensions.Cmd(db,sql,tx,args);
    return await cmd.ExecuteScalarAsync();
}
static async Task<int> ExecTx(SqliteConnection db,SqliteTransaction tx,string sql,params (string,object?)[] args)
{
    await using var cmd=DbExtensions.Cmd(db,sql,tx,args);
    return await cmd.ExecuteNonQueryAsync();
}
static async Task<string> PremiumUntilTx(SqliteConnection db,SqliteTransaction tx,string id)
{
    await using var cmd=DbExtensions.Cmd(db,"SELECT MAX(ends_at) FROM subscriptions WHERE user_id=@id",tx,("@id",id));
    return Convert.ToString(await cmd.ExecuteScalarAsync()) ?? "";
}
static async Task RunProgram(string executable,IEnumerable<string> arguments)
{
    using var proc=new Process();
    proc.StartInfo.FileName=executable;
    proc.StartInfo.UseShellExecute=false;
    proc.StartInfo.RedirectStandardError=true;
    proc.StartInfo.RedirectStandardOutput=true;
    foreach(var arg in arguments) proc.StartInfo.ArgumentList.Add(arg);
    if (!proc.Start()) throw new IOException("Không thể khởi động FFmpeg.");
    var errTask=proc.StandardError.ReadToEndAsync();
    var outTask=proc.StandardOutput.ReadToEndAsync();
    using var cts=new CancellationTokenSource(TimeSpan.FromMinutes(15));
    try { await proc.WaitForExitAsync(cts.Token); }
    catch (OperationCanceledException) { proc.Kill(entireProcessTree:true); throw new TimeoutException("FFmpeg xử lý quá 15 phút."); }
    var err=await errTask; await outTask;
    if(proc.ExitCode!=0) throw new InvalidDataException("FFmpeg xử lý thất bại: "+(err.Length>400?err[^400..]:err));
}
static async Task<int> ProbeDuration(string fullPath)
{
    var exe=Environment.GetEnvironmentVariable("FFPROBE_PATH")??"ffprobe";
    using var proc=new Process();
    proc.StartInfo.FileName=exe; proc.StartInfo.UseShellExecute=false;
    proc.StartInfo.RedirectStandardOutput=true; proc.StartInfo.RedirectStandardError=true;
    foreach(var arg in new[]{"-v","error","-show_entries","format=duration","-of","default=noprint_wrappers=1:nokey=1",fullPath}) proc.StartInfo.ArgumentList.Add(arg);
    if(!proc.Start()) throw new IOException("Không tìm thấy ffprobe.");
    var outputTask=proc.StandardOutput.ReadToEndAsync();
    var errorTask=proc.StandardError.ReadToEndAsync();
    using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try { await proc.WaitForExitAsync(cts.Token); }
    catch(OperationCanceledException){proc.Kill(entireProcessTree:true);throw new TimeoutException("ffprobe quá thời gian.");}
    var output=await outputTask; await errorTask;
    if(proc.ExitCode!=0 || !double.TryParse(output.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out var seconds)) throw new InvalidDataException("Không đọc được thời lượng MP3.");
    return (int)Math.Ceiling(seconds);
}
static void DeleteFiles(params string[] files)
{
    foreach(var path in files) { try { if(File.Exists(path)) File.Delete(path); } catch(IOException){} }
}
static async Task SeedAdminAsync(IServiceProvider services,Database source)
{
    // The bundled initial credentials are for Local/Development ONLY.
    // The password is NOT stored as plaintext in the distribution: below is an
    // ASP.NET Core Identity V3 PBKDF2 password hash (HMAC-SHA512, 100000 rounds).
    // Before exposing a copied Development database to the Internet, change its password.
    var environment = services.GetRequiredService<IWebHostEnvironment>();
    // In Production only explicitly authorized, one-time bootstrap from env variables.
    var bootstrapProduction = Environment.GetEnvironmentVariable("LKH_BOOTSTRAP_ADMIN_ONCE") == "1";
    if (!environment.IsDevelopment() && !bootstrapProduction) return;

    var username = Environment.GetEnvironmentVariable("LKH_ADMIN_USERNAME");
    var password = Environment.GetEnvironmentVariable("LKH_ADMIN_PASSWORD");
    var customCredentials = !string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(password);
    if (!environment.IsDevelopment() && !customCredentials)
        throw new InvalidOperationException("Production Admin bootstrap requires both LKH_ADMIN_USERNAME and LKH_ADMIN_PASSWORD.");
    if (customCredentials && (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)))
        throw new InvalidOperationException("Phải khai báo cả LKH_ADMIN_USERNAME và LKH_ADMIN_PASSWORD.");

    username = customCredentials ? username!.Trim().ToLowerInvariant() : "admin";
    if (!Regex.IsMatch(username,"^[a-z0-9_]{3,30}$"))
        throw new InvalidOperationException("Username Admin không hợp lệ.");
    if (customCredentials && password!.Length < 12)
        throw new InvalidOperationException("Mật khẩu Admin tùy chỉnh phải từ 12 ký tự.");

    await using var db = await source.OpenAsync();
    // IMPORTANT: do not overwrite an existing Admin account or reset its password.
    if (await db.ValueAsync("SELECT 1 FROM users WHERE role='Admin' LIMIT 1") is not null) return;
    if (await db.ValueAsync("SELECT 1 FROM users WHERE username=@name",("@name",username)) is not null)
        throw new InvalidOperationException("Username Admin đã được đăng ký với quyền User.");

    var admin = new AppUser { Id=Helpers.Id(), Username=username };
    const string developmentAdminPasswordHash = "AQAAAAIAAYagAAAAEBWFTXPbqN9JBzQgvrEQMX+XprLPfLx9Pmt0jTsif3K907RzQaUjyIAcTPSK2mILqA==";
    var hash = customCredentials
        ? services.GetRequiredService<IPasswordHasher<AppUser>>().HashPassword(admin,password!)
        : developmentAdminPasswordHash;

    await db.ExecAsync("INSERT INTO users(id,username,password_hash,full_name,role,created_at,updated_at) VALUES(@id,@u,@pwd,'Administrator','Admin',@now,@now)",
      ("@id",admin.Id),("@u",admin.Username),("@pwd",hash),("@now",Helpers.Now()));
    await Helpers.Audit(db,admin.Id,"admin.bootstrap",admin.Id,
        customCredentials ? "Initial Development Admin from environment variables" : "Initial Development Admin from bundled seed hash");
    Console.WriteLine($"[Bootstrap] Đã tạo tài khoản Admin '{username}'. Hãy đổi mật khẩu trước khi triển khai website.");
}
public sealed class AppUser { public string Id {get;set;}=""; public string Username {get;set;}=""; }
public record RegisterReq(string Username,string Password,string? FullName,string? Phone,string? Address);
public record LoginReq(string Username,string Password);
public record ProfileReq(string FullName,string Phone,string Address);
public record ChangePasswordReq(string CurrentPassword,string NewPassword);
public record AdminUserReq(string FullName,string Phone,string Address,string Status);
public record ProgressReq(int Seconds);
public record StoryReq(string Slug,string Title,string Author,string Genre,string Description,string CoverUrl,string Status,string[]? Genres = null);
public record GenreReq(string Name,bool IsActive = true);
public record ReviewReq(int Stars,string Body);
public record ReviewVisibilityReq(string Status);
public record HeartbeatReq(string SessionId);
public record EpisodeReq(string Title,int EpisodeNumber,string Status);
public record GrantReq(int Months,string Reason);
public record PriceReq(int PriceVnd);
public record BankReq(string BankBin,string BankName,string AccountNumber,string AccountName);
public record ConfirmReq(string BankReference);
public record RejectReq(string Reason);

public record BankAccountReq(string Label,string BankBin,string BankName,string AccountNumber,string AccountName);
public record BankActivationReq(bool IsActive);
public record TextCreateReq(string Title, string FileName, string Extension, string Content, string? StorageTarget = "vps");
public record TextUpdateReq(string StoryId, string Title, string FileName, string Extension, string Content, string? StorageTarget = null);
public record TextTransferReq(string Target);
public record GDriveConfigReq(string? ClientId, string? ClientSecret, string? RootFolderId, string? RootFolderName);
public record GDriveFolderCreateReq(string? ParentFolderId, string Name);
public record GDriveFileSaveReq(string? DriveFileId, string? ParentFolderId, string FileName, string Title, string Content);
