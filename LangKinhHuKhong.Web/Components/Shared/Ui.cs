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
    public static string OrderStatus(string? status) => (status ?? "").Trim() switch
    {
        "Pending" => "Chờ chuyển khoản",
        "Reviewing" => "Chờ đối soát",
        "Paid" => "Đã thanh toán",
        "Rejected" => "Đã từ chối",
        "Expired" => "Đã hết hạn",
        "Cancelled" => "Đã hủy",
        "Refunded" => "Đã hoàn tiền",
        _ => string.IsNullOrWhiteSpace(status) ? "—" : status
    };
    public static string OrderStatusBadgeClass(string? status) => (status ?? "").Trim() switch
    {
        "Paid" => "storage-r2",
        "Pending" or "Reviewing" => "pill",
        _ => "storage-local"
    };
    public static string UserStatus(string? status) => (status ?? "").Trim() switch
    {
        "Active" => "Hoạt động",
        "Locked" => "Đã khóa",
        "Deleted" => "Đã xóa",
        _ => string.IsNullOrWhiteSpace(status) ? "—" : status
    };
    public static string EpisodeStatus(string? status) => (status ?? "").Trim() switch
    {
        "Published" => "Phát hành",
        "Hidden" => "Đang ẩn",
        "Draft" => "Bản nháp",
        _ => string.IsNullOrWhiteSpace(status) ? "—" : status
    };
    public static string ReviewStatus(string? status) => (status ?? "").Trim() switch
    {
        "Visible" => "Đang hiển thị",
        "Hidden" => "Đã ẩn",
        _ => string.IsNullOrWhiteSpace(status) ? "—" : status
    };
    public static string SePayStatus(string? status) => (status ?? "").Trim() switch
    {
        "Confirmed" => "Đã duyệt tự động",
        "AlreadyPaid" or "AlreadyProcessed" => "Đã thanh toán trước đó",
        "Underpaid" => "Chuyển thiếu tiền",
        "OrderNotFound" => "Không khớp mã đơn",
        "IgnoredOut" => "Giao dịch tiền ra (bỏ qua)",
        "InvalidSignature" => "Sai chữ ký HMAC",
        "InvalidJson" => "Dữ liệu JSON lỗi",
        _ => string.IsNullOrWhiteSpace(status) ? "—" : status
    };
}
