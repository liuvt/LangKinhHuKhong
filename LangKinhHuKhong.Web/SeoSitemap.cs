using System.Globalization;
using System.Security;
using System.Text;

namespace LangKinhHuKhong;

public static class SeoSitemap
{
    // Sitemap index partitions keep each child comfortably below Google's 50,000 URL limit.
    private const int PerPage = 10000;
    private const string Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static async Task<string> Index(Database source, SeoSiteSettings site)
    {
        await using var db = await source.OpenAsync();
        var total = Convert.ToInt32(await db.ValueAsync("SELECT COUNT(*) FROM stories WHERE status='Published'"), CultureInfo.InvariantCulture);
        var b = Begin("sitemapindex");
        Item(b, "sitemap", "loc", site.Absolute("/sitemap-static.xml"));
        for (var p = 1; p <= (total + PerPage - 1) / PerPage; p++)
            Item(b, "sitemap", "loc", site.Absolute("/sitemap-stories-" + p + ".xml"));
        return End(b, "sitemapindex");
    }

    public static async Task<string> Static(Database source, SeoSiteSettings site)
    {
        await using var db = await source.OpenAsync();
        var b = Begin("urlset");
        Url(b, site.Absolute("/"));
        Url(b, site.Absolute("/thu-vien"));
        Url(b, site.Absolute("/premium"));
        var genres = await db.ListAsync(@"SELECT g.slug FROM genres g WHERE g.is_active=1 AND EXISTS(
            SELECT 1 FROM story_genres sg JOIN stories s ON s.id=sg.story_id
            WHERE sg.genre_slug=g.slug AND s.status='Published') ORDER BY g.slug");
        foreach (var genre in genres)
            Url(b, site.Absolute("/the-loai/" + Uri.EscapeDataString(genre.S("slug"))));
        return End(b, "urlset");
    }

    public static async Task<string?> Stories(Database source, SeoSiteSettings site, int page)
    {
        if (page < 1) return null;
        await using var db = await source.OpenAsync();
        var total = Convert.ToInt32(await db.ValueAsync("SELECT COUNT(*) FROM stories WHERE status='Published'"), CultureInfo.InvariantCulture);
        if ((long)(page - 1) * PerPage >= total) return null;
        var list = await db.ListAsync(@"SELECT s.slug,
            CASE WHEN COALESCE((SELECT MAX(e.created_at) FROM episodes e
              WHERE e.story_id=s.id AND e.status='Published'),'') > s.updated_at
              THEN (SELECT MAX(e.created_at) FROM episodes e WHERE e.story_id=s.id AND e.status='Published')
              ELSE s.updated_at END AS updated_at
            FROM stories s WHERE s.status='Published'
            ORDER BY s.slug LIMIT @limit OFFSET @offset", ("@limit", PerPage), ("@offset", (page - 1) * PerPage));
        var b = Begin("urlset");
        foreach (var story in list)
        {
            var modified = DateTimeOffset.TryParse(story.S("updated_at"), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d) ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            Url(b, site.Absolute("/truyen/" + Uri.EscapeDataString(story.S("slug"))), modified);
        }
        return End(b, "urlset");
    }

    private static StringBuilder Begin(string name) => new StringBuilder(4096)
        .Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<").Append(name)
        .Append(" xmlns=\"").Append(Ns).Append("\">\n");
    private static string End(StringBuilder b, string name) => b.Append("</").Append(name).Append(">\n").ToString();
    private static void Item(StringBuilder b, string tag, string key, string value) =>
        b.Append('<').Append(tag).Append("><").Append(key).Append('>')
         .Append(E(value)).Append("</").Append(key).Append("></").Append(tag).Append(">\n");
    private static void Url(StringBuilder b, string loc, string? lastmod = null)
    {
        b.Append("<url><loc>").Append(E(loc)).Append("</loc>");
        if (lastmod is not null) b.Append("<lastmod>").Append(lastmod).Append("</lastmod>");
        b.Append("</url>\n");
    }
    private static string E(string value) => SecurityElement.Escape(value) ?? "";
}
