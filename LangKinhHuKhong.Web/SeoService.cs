using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LangKinhHuKhong;

/// <summary>
/// Search metadata is built from published database records during SSR.
/// Never derive canonical URLs from an untrusted Host / X-Forwarded-Host header.
/// </summary>
public sealed record SeoSiteSettings(string Origin)
{
    public string Absolute(string path) => Origin.TrimEnd('/') + (path.StartsWith('/') ? path : "/" + path);
}

public sealed record SeoDocument(
    string Title, string Description, string Canonical, string Image,
    string Robots, string OgType, string JsonLd, string Keywords = "")
{
    public static SeoDocument Unavailable(SeoSiteSettings site, string title = "Trang không khả dụng") =>
        new(title + " | Lăng Kính Hư Không", "", site.Absolute("/"),
            site.Absolute("/images/lkh-social.png"), "noindex,nofollow", "website", "");
}

public sealed class SeoService(Database source, SeoSiteSettings site)
{
    public async Task<SeoDocument> GetAsync(string route)
    {
        route = (route ?? "").Trim('/');
        if (route == "" || route.Equals("thu-vien", StringComparison.OrdinalIgnoreCase))
            return Basic(route);

        await using var db = await source.OpenAsync();
        if (route.StartsWith("the-loai/", StringComparison.OrdinalIgnoreCase))
        {
            var slug = Uri.UnescapeDataString(route["the-loai/".Length..]);
            var item = await db.FirstAsync("SELECT slug,name FROM genres WHERE slug=@slug AND is_active=1", ("@slug", slug));
            if (item is null) return SeoDocument.Unavailable(site, "Thể loại không tồn tại");
            var hasPublished = Convert.ToInt32(await db.ValueAsync(@"SELECT EXISTS(
                SELECT 1 FROM story_genres sg JOIN stories s ON s.id=sg.story_id
                WHERE sg.genre_slug=@slug AND s.status='Published')", ("@slug", slug)), CultureInfo.InvariantCulture) > 0;
            if (!hasPublished) return SeoDocument.Unavailable(site, "Thể loại chưa có truyện");
            var genre = item.S("name");
            var canonical = site.Absolute("/the-loai/" + Uri.EscapeDataString(item.S("slug")));
            var description = Short($"Nghe truyện {genre} audio tại Lăng Kính Hư Không. Đọc giới thiệu truyện audio, khám phá tác giả và chọn tập để nghe trực tuyến.", 165);
            return Make($"Nghe truyện {genre} audio | Lăng Kính Hư Không",
                description, canonical, "CollectionPage", "website", null,
                ["Thư viện truyện", "/thu-vien", "Thể loại " + genre, "/the-loai/" + item.S("slug")],
                keywords: $"nghe truyện {genre} Lăng Kính Hư Không, nghe audio {genre}, truyện audio {genre}, Lăng Kính Hư Không");
        }

        if (route.StartsWith("truyen/", StringComparison.OrdinalIgnoreCase))
        {
            var slug = Uri.UnescapeDataString(route["truyen/".Length..]);
            var story = await db.FirstAsync(@"SELECT s.id,s.slug,s.title,s.author,s.description,s.created_at,s.updated_at,s.genre,
                CASE WHEN s.cover_file<>'' THEN '/api/stories/' || s.id || '/cover' ELSE s.cover_url END AS cover_url,
                COALESCE((SELECT group_concat(g.name,' | ') FROM story_genres sg JOIN genres g ON g.slug=sg.genre_slug WHERE sg.story_id=s.id),'') AS genres,
                (SELECT COUNT(*) FROM story_reviews r WHERE r.story_id=s.id AND r.status='Visible') AS ratingCount,
                COALESCE((SELECT ROUND(AVG(r.stars),1) FROM story_reviews r WHERE r.story_id=s.id AND r.status='Visible'),0) AS ratingAverage
                FROM stories s WHERE s.slug=@slug AND s.status='Published'", ("@slug", slug));
            if (story is null) return SeoDocument.Unavailable(site, "Không tìm thấy truyện");
            var name = story.S("title");
            var author = story.S("author");
            var genres = story.S("genres");
            var intro = Plain(story.S("description"));
            // Keep the brand in title and description even when the original synopsis is very long.
            var summary = string.IsNullOrWhiteSpace(intro) ? "Đọc giới thiệu, chọn tập và nghe audio trực tuyến."
                : Short(intro, 90);
            var description = Short($"Nghe truyện audio {name} tại Lăng Kính Hư Không. {summary}", 170);
            var title = Short(name, 55) + " – Nghe truyện audio | Lăng Kính Hư Không";
            var canonical = site.Absolute("/truyen/" + Uri.EscapeDataString(story.S("slug")));
            var image = StoryImage(story.S("cover_url"));
            var book = new Dictionary<string, object?>
            {
                ["@type"] = "Audiobook", ["@id"] = canonical + "#audiobook",
                ["url"] = canonical, ["name"] = name, ["description"] = description,
                ["inLanguage"] = "vi", ["bookFormat"] = "https://schema.org/AudiobookFormat",
                ["image"] = image, ["publisher"] = new Dictionary<string, object> { ["@id"] = site.Absolute("/#organization") }
            };
            if (!string.IsNullOrWhiteSpace(author))
                book["author"] = new Dictionary<string, object> { ["@type"] = "Person", ["name"] = author };
            if (!string.IsNullOrWhiteSpace(genres)) book["genre"] = genres.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (Date(story.S("created_at")) is { } published) book["datePublished"] = published;
            if (Date(story.S("updated_at")) is { } modified) book["dateModified"] = modified;
            var reviewCount = story.N("ratingCount");
            var rating = Convert.ToDouble(story.GetValueOrDefault("ratingAverage") ?? 0, CultureInfo.InvariantCulture);
            if (reviewCount > 0 && rating is > 0 and <= 5)
                book["aggregateRating"] = new Dictionary<string, object> { ["@type"] = "AggregateRating", ["ratingValue"] = rating, ["ratingCount"] = reviewCount, ["bestRating"] = 5, ["worstRating"] = 1 };
            return Make(title, description, canonical, "WebPage", "book", book,
                ["Thư viện truyện", "/thu-vien", name, "/truyen/" + story.S("slug")], image,
                keywords: $"nghe truyện {name}, nghe audio {name}, truyện audio {name}, đọc truyện audio Lăng Kính Hư Không, Lăng Kính Hư Không");
        }

        // Personal account, admin, listening history, etc. must not be indexed.
        if (route is "lich-su" or "tai-khoan" or "quan-tri")
            return SeoDocument.Unavailable(site, "Khu vực thành viên");
        if (route == "premium")
        {
            var u = site.Absolute("/premium");
            return Make("Nghe audio trọn bộ Premium | Lăng Kính Hư Không",
                "Nghe truyện audio trọn bộ với Premium tại Lăng Kính Hư Không. Tiếp tục tập đang nghe, nghe audio dài không giới hạn preview 5 phút.",
                u, "WebPage", "website", keywords: "Premium Lăng Kính Hư Không, nghe audio Lăng Kính Hư Không, truyện audio trọn bộ");
        }
        return SeoDocument.Unavailable(site);
    }

    private SeoDocument Basic(string route)
    {
        var home = route.Length == 0;
        var title = home ? "Nghe Truyện Audio Online | Lăng Kính Hư Không"
            : "Truyện Audio – Nghe Audio Theo Thể Loại | Lăng Kính Hư Không";
        var desc = home
            ? "Lăng Kính Hư Không: nghe truyện, nghe audio truyện tiếng Việt online. Truyện audio tiên hiệp, kiếm hiệp, xuyên không và huyền huyễn; nghe thử miễn phí."
            : "Đọc giới thiệu truyện audio, tìm tác giả và nghe audio trực tuyến tại Lăng Kính Hư Không. Thư viện truyện audio theo thể loại, cập nhật tập mới.";
        var keywords = home
            ? "nghe truyện Lăng Kính Hư Không, nghe audio Lăng Kính Hư Không, truyện audio Lăng Kính Hư Không, đọc truyện audio Lăng Kính Hư Không, nghe truyện online"
            : "truyện audio Lăng Kính Hư Không, đọc truyện audio Lăng Kính Hư Không, nghe truyện online, nghe audio theo thể loại";
        var url = site.Absolute(home ? "/" : "/thu-vien");
        return Make(title, desc, url, home ? "WebPage" : "CollectionPage", "website", keywords: keywords);
    }

    private SeoDocument Make(string title, string description, string canonical,
        string pageType, string ogType, Dictionary<string, object?>? book = null,
        string[]? breadcrumb = null, string? image = null, string? keywords = null)
    {
        image ??= site.Absolute("/images/lkh-social.png");
        var nodes = new List<object>
        {
            new Dictionary<string, object>
            {
                ["@type"] = "Organization", ["@id"] = site.Absolute("/#organization"),
                ["name"] = "Lăng Kính Hư Không", ["alternateName"] = "LKHK", ["url"] = site.Absolute("/"),
                ["description"] = "Lăng Kính Hư Không – nền tảng nghe truyện audio tiếng Việt trực tuyến.",
                ["logo"] = new Dictionary<string, object> { ["@type"] = "ImageObject", ["url"] = site.Absolute("/images/lkh-logo.png"), ["width"] = 512, ["height"] = 512 }
            },
            new Dictionary<string, object>
            {
                ["@type"] = "WebSite", ["@id"] = site.Absolute("/#website"),
                ["name"] = "Lăng Kính Hư Không", ["alternateName"] = "LKHK", ["url"] = site.Absolute("/"),
                ["description"] = "Nghe truyện audio online, đọc giới thiệu và khám phá truyện mới tại Lăng Kính Hư Không.",
                ["inLanguage"] = "vi", ["publisher"] = new Dictionary<string, object> { ["@id"] = site.Absolute("/#organization") }
            },
            new Dictionary<string, object>
            {
                ["@type"] = pageType, ["@id"] = canonical + "#webpage",
                ["url"] = canonical, ["name"] = title, ["description"] = description,
                ["inLanguage"] = "vi", ["isPartOf"] = new Dictionary<string, object> { ["@id"] = site.Absolute("/#website") }
            }
        };
        if (book is not null) nodes.Add(book);
        if (breadcrumb is { Length: 4 })
        {
            nodes.Add(new Dictionary<string, object>
            {
                ["@type"] = "BreadcrumbList", ["@id"] = canonical + "#breadcrumb",
                ["itemListElement"] = new object[]
                {
                    new Dictionary<string, object> { ["@type"] = "ListItem", ["position"] = 1, ["name"] = "Trang chủ", ["item"] = site.Absolute("/") },
                    new Dictionary<string, object> { ["@type"] = "ListItem", ["position"] = 2, ["name"] = breadcrumb[0], ["item"] = site.Absolute(breadcrumb[1]) },
                    new Dictionary<string, object> { ["@type"] = "ListItem", ["position"] = 3, ["name"] = breadcrumb[2], ["item"] = site.Absolute(breadcrumb[3]) }
                }
            });
        }
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
            { ["@context"] = "https://schema.org", ["@graph"] = nodes });
        return new SeoDocument(title, description, canonical, image,
            "index,follow,max-image-preview:large", ogType, json, keywords ?? "Lăng Kính Hư Không");
    }

    private string StoryImage(string raw)
    {
        if (raw.StartsWith('/') && !raw.StartsWith("//")) return site.Absolute(raw);
        if (Uri.TryCreate(raw, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps)
            return url.ToString();
        return site.Absolute("/images/lkh-social.png");
    }

    private static string? Date(string raw) => DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var value) ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;

    private static string Plain(string value) => Regex.Replace(Regex.Replace(value ?? "", "<[^>]*>", " "), @"\s+", " ").Trim();
    private static string Short(string value, int limit)
    {
        value = Plain(value);
        if (value.Length <= limit) return value;
        var cut = value[..limit].LastIndexOf(' ');
        return value[..(cut > limit / 2 ? cut : limit)].TrimEnd(' ', '.', ',') + "…";
    }
}
