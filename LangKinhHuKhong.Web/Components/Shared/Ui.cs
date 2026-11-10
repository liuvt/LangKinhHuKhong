using System.Globalization;
using System.Text.Json;

namespace LangKinhHuKhong.Components.Shared;

public static class Ui
{
    public static JsonElement Property(JsonElement item, string key)
        => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var result) ? result : default;
    public static string Str(JsonElement item, string key)
    {
        var prop = Property(item,key);
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? "",
            JsonValueKind.Number => prop.ToString(),
            _ => ""
        };
    }
    public static bool Bool(JsonElement item,string key) => Property(item,key).ValueKind == JsonValueKind.True;
    public static int Num(JsonElement item,string key)
    {
        var prop=Property(item,key);
        return prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var n) ? n : 0;
    }
    public static JsonElement[] Array(JsonElement value)
        => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [];
    public static string Money(int amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";
    public static string Date(string input)
        => DateTimeOffset.TryParse(input, out var d) ? d.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "—";
    public static string TimeAgo(string input)
    {
        if (!DateTimeOffset.TryParse(input, out var d)) return "";
        var diff = DateTimeOffset.UtcNow - d.ToUniversalTime();
        if (diff.TotalMinutes < 1) return "Vừa xong";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} phút trước";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} giờ trước";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} ngày trước";
        return d.ToLocalTime().ToString("dd/MM/yyyy");
    }
    public static string Duration(int seconds)
    {
        var s = Math.Max(0, seconds);
        var h = s / 3600;
        var m = (s % 3600) / 60;
        var ss = s % 60;
        return h > 0 ? $"{h}:{m:00}:{ss:00}" : $"{m:00}:{ss:00}";
    }
    public static string FileSize(int bytes)
    {
        var b = Math.Max(0, bytes);
        if (b >= 1024 * 1024) return (b / (1024.0 * 1024.0)).ToString("0.##", CultureInfo.GetCultureInfo("vi-VN")) + " MB";
        if (b >= 1024) return (b / 1024.0).ToString("0.#", CultureInfo.GetCultureInfo("vi-VN")) + " KB";
        return b + " B";
    }
}
