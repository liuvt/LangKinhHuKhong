using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace LangKinhHuKhong;

public static class SePayWebhook
{
    public const string WebhookPath = "/lkhk-v1/sepay-payment";
    private const string SecretFileName = "sepay-webhook.secret";

    public static string ResolveSecret(Database source, IConfiguration config)
    {
        var secretFile = Path.Combine(source.DataRoot, SecretFileName);
        if (File.Exists(secretFile))
        {
            try
            {
                var fromFile = File.ReadAllText(secretFile, Encoding.UTF8).Trim();
                if (!string.IsNullOrWhiteSpace(fromFile))
                    return fromFile;
            }
            catch { }
        }

        var envSecret = Environment.GetEnvironmentVariable("LKH_SEPAY_WEBHOOK_SECRET")
                        ?? config["LKH_SEPAY_WEBHOOK_SECRET"];
        if (!string.IsNullOrWhiteSpace(envSecret))
            return envSecret.Trim();

        return "";
    }

    public static async Task SaveSecretAsync(Database source, string secret)
    {
        Directory.CreateDirectory(source.DataRoot);
        var secretFile = Path.Combine(source.DataRoot, SecretFileName);
        await File.WriteAllTextAsync(secretFile, (secret ?? "").Trim(), new UTF8Encoding(false));
    }

    public static object GetStatus(Database source, IConfiguration config, SeoSiteSettings site)
    {
        var secret = ResolveSecret(source, config);
        var masked = "";
        if (!string.IsNullOrWhiteSpace(secret))
        {
            masked = secret.Length <= 10
                ? new string('*', secret.Length)
                : $"{secret[..8]}••••••••{secret[^4..]}";
        }
        return new
        {
            webhookUrl = site.Absolute(WebhookPath),
            path = WebhookPath,
            authMethod = "HMAC-SHA256",
            signatureHeader = "X-SePay-Signature",
            configured = !string.IsNullOrWhiteSpace(secret),
            maskedSecret = masked
        };
    }

    /// <summary>
    /// Verifies SePay's X-SePay-Signature header using HMAC-SHA256 in constant time.
    /// Supports:
    /// 1) Header X-SePay-Signature: "sha256={hex}" or "{hex}" or "{base64}" over raw body bytes
    /// 2) Header X-SePay-Signature + X-SePay-Timestamp over "{timestamp}.{rawBody}"
    /// 3) Header X-SePay-Signature: "t={timestamp},v1={hex}" or "t={timestamp},sha256={hex}" over "{timestamp}.{rawBody}"
    /// </summary>
    public static bool VerifyHmacSignature(
        byte[] rawBodyBytes,
        string rawBodyText,
        string? signatureHeader,
        string? timestampHeader,
        string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        var sigRaw = signatureHeader.Trim();
        var inlineTimestamp = "";
        var candidateTokens = new List<string>();

        if (sigRaw.Contains(','))
        {
            foreach (var part in sigRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
                if (kv.Length == 2)
                {
                    if (kv[0].Equals("t", StringComparison.OrdinalIgnoreCase) ||
                        kv[0].Equals("timestamp", StringComparison.OrdinalIgnoreCase))
                    {
                        inlineTimestamp = kv[1];
                    }
                    else if (kv[0].Equals("v1", StringComparison.OrdinalIgnoreCase) ||
                             kv[0].Equals("sha256", StringComparison.OrdinalIgnoreCase) ||
                             kv[0].Equals("sig", StringComparison.OrdinalIgnoreCase) ||
                             kv[0].Equals("signature", StringComparison.OrdinalIgnoreCase))
                    {
                        candidateTokens.Add(kv[1]);
                    }
                }
            }
        }

        if (candidateTokens.Count == 0)
        {
            var token = sigRaw;
            if (token.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
                token = token["sha256=".Length..].Trim();
            else if (token.StartsWith("hmac-sha256=", StringComparison.OrdinalIgnoreCase))
                token = token["hmac-sha256=".Length..].Trim();
            else if (token.StartsWith("v1=", StringComparison.OrdinalIgnoreCase))
                token = token["v1=".Length..].Trim();
            candidateTokens.Add(token);
        }

        var providedHashes = new List<byte[]>();
        foreach (var tok in candidateTokens)
        {
            var cleanTok = tok.Trim().Trim('"');
            if (cleanTok.Length == 64 && IsHex(cleanTok))
            {
                try { providedHashes.Add(Convert.FromHexString(cleanTok)); } catch { }
            }
            else if (cleanTok.Length is >= 43 and <= 48)
            {
                try
                {
                    var normalizedB64 = cleanTok.Replace('-', '+').Replace('_', '/');
                    switch (normalizedB64.Length % 4)
                    {
                        case 2: normalizedB64 += "=="; break;
                        case 3: normalizedB64 += "="; break;
                    }
                    var b64 = Convert.FromBase64String(normalizedB64);
                    if (b64.Length == 32) providedHashes.Add(b64);
                }
                catch { }
            }
        }

        if (providedHashes.Count == 0)
            return false;

        var secretKeys = new List<byte[]> { Encoding.UTF8.GetBytes(secret) };
        if (secret.StartsWith("whsec_", StringComparison.OrdinalIgnoreCase) && secret.Length > 6)
        {
            var stripped = secret[6..];
            secretKeys.Add(Encoding.UTF8.GetBytes(stripped));
        }

        var payloadsToSign = new List<byte[]> { rawBodyBytes };
        var trimmedBody = rawBodyText.Trim();
        if (trimmedBody.Length != rawBodyText.Length)
            payloadsToSign.Add(Encoding.UTF8.GetBytes(trimmedBody));

        var ts = !string.IsNullOrWhiteSpace(timestampHeader) ? timestampHeader.Trim() : inlineTimestamp;
        if (!string.IsNullOrWhiteSpace(ts))
        {
            payloadsToSign.Add(Encoding.UTF8.GetBytes($"{ts}.{rawBodyText}"));
            payloadsToSign.Add(Encoding.UTF8.GetBytes($"{ts}:{rawBodyText}"));
            if (trimmedBody.Length != rawBodyText.Length)
                payloadsToSign.Add(Encoding.UTF8.GetBytes($"{ts}.{trimmedBody}"));
        }

        var matched = false;
        foreach (var keyBytes in secretKeys)
        {
            foreach (var payloadBytes in payloadsToSign)
            {
                var computed = HMACSHA256.HashData(keyBytes, payloadBytes);
                foreach (var provided in providedHashes)
                {
                    if (CryptographicOperations.FixedTimeEquals(computed, provided))
                        matched = true;
                }
            }
        }

        return matched;
    }

    private static bool IsHex(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            var ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!ok) return false;
        }
        return true;
    }

    public static async Task<IResult> HandleWebhookAsync(HttpContext ctx, Database source, IConfiguration config)
    {
        if (ctx.Request.ContentLength is > 256 * 1024)
            return Results.Json(new { success = false, error = "Payload too large" }, statusCode: StatusCodes.Status413PayloadTooLarge);

        byte[] rawBytes;
        await using (var ms = new MemoryStream())
        {
            await ctx.Request.Body.CopyToAsync(ms, ctx.RequestAborted);
            if (ms.Length > 256 * 1024)
                return Results.Json(new { success = false, error = "Payload too large" }, statusCode: StatusCodes.Status413PayloadTooLarge);
            rawBytes = ms.ToArray();
        }

        var rawBody = Encoding.UTF8.GetString(rawBytes);
        var signatureHeader = ctx.Request.Headers["X-SePay-Signature"].FirstOrDefault()
                              ?? ctx.Request.Headers["X-Sepay-Signature"].FirstOrDefault()
                              ?? ctx.Request.Headers["X-Webhook-Signature"].FirstOrDefault();
        var timestampHeader = ctx.Request.Headers["X-SePay-Timestamp"].FirstOrDefault()
                              ?? ctx.Request.Headers["X-Sepay-Timestamp"].FirstOrDefault();

        var secret = ResolveSecret(source, config);
        if (string.IsNullOrWhiteSpace(secret))
        {
            return Results.Json(new { success = false, error = "Webhook secret is not configured on server" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!VerifyHmacSignature(rawBytes, rawBody, signatureHeader, timestampHeader, secret))
        {
            await TryLogWebhookAsync(source, "", "", "", "", "", 0, "", "", "", "", "", "InvalidSignature", rawBody);
            return Results.Json(new { success = false, error = "Invalid X-SePay-Signature" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(rawBody))
            return Results.Json(new { success = false, error = "Empty JSON body" }, statusCode: StatusCodes.Status400BadRequest);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch
        {
            await TryLogWebhookAsync(source, "", "", "", "", "", 0, "", "", "", "", "", "InvalidJson", rawBody);
            return Results.Json(new { success = false, error = "Invalid JSON payload" }, statusCode: StatusCodes.Status400BadRequest);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Results.Json(new { success = false, error = "Root JSON must be an object" }, statusCode: StatusCodes.Status400BadRequest);

            var sepayId = ReadStr(root, "id");
            var gateway = ReadStr(root, "gateway");
            var transactionDate = ReadStr(root, "transactionDate");
            var accountNumber = ReadStr(root, "accountNumber");
            var paymentCode = ReadStr(root, "code");
            var content = ReadStr(root, "content");
            var description = ReadStr(root, "description");
            var transferType = ReadStr(root, "transferType").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(transferType)) transferType = "in";
            var transferAmount = ReadLong(root, "transferAmount");
            if (transferAmount <= 0) transferAmount = ReadLong(root, "amount");
            var referenceCode = ReadStr(root, "referenceCode");

            // Ignore outgoing transactions ("out"), return 200 OK so SePay does not retry.
            if (transferType == "out")
            {
                await TryLogWebhookAsync(source, sepayId, gateway, transactionDate, accountNumber, transferType,
                    (int)Math.Clamp(transferAmount, 0, int.MaxValue), referenceCode, paymentCode, content, "", "", "IgnoredOut", rawBody);
                return Results.Ok(new { success = true, status = "IgnoredOut" });
            }

            await using var db = await source.OpenAsync();

            // Check if this exact SePay transaction ID was already processed into payments
            if (!string.IsNullOrWhiteSpace(sepayId))
            {
                var existingLog = await db.FirstAsync(
                    "SELECT matched_order_code, process_status FROM sepay_webhook_logs WHERE sepay_tx_id=@tx AND process_status='Confirmed' LIMIT 1",
                    ("@tx", sepayId));
                if (existingLog is not null)
                {
                    return Results.Ok(new
                    {
                        success = true,
                        status = "AlreadyProcessed",
                        orderCode = existingLog.S("matched_order_code")
                    });
                }
            }

            // Match order by code inside `code`, `content`, or `description`
            var combinedText = $"{paymentCode} {content} {description}";
            var matchedOrder = await FindMatchingOrderAsync(db, combinedText);
            if (matchedOrder is null)
            {
                await TryLogWebhookAsync(source, sepayId, gateway, transactionDate, accountNumber, transferType,
                    (int)Math.Clamp(transferAmount, 0, int.MaxValue), referenceCode, paymentCode, content, "", "", "OrderNotFound", rawBody);
                return Results.Ok(new { success = true, status = "OrderNotFound" });
            }

            var orderId = matchedOrder.S("id");
            var orderCode = matchedOrder.S("code");
            var orderStatus = matchedOrder.S("status");
            var requiredAmount = matchedOrder.N("amount_vnd");

            if (orderStatus == "Paid")
            {
                await TryLogWebhookAsync(source, sepayId, gateway, transactionDate, accountNumber, transferType,
                    (int)Math.Clamp(transferAmount, 0, int.MaxValue), referenceCode, paymentCode, content, orderId, orderCode, "AlreadyPaid", rawBody);
                return Results.Ok(new { success = true, status = "AlreadyPaid", orderCode });
            }

            if (transferAmount < requiredAmount)
            {
                var underpaidNote = $"SePay nhận {transferAmount:N0}đ / cần {requiredAmount:N0}đ (GD #{sepayId}, Ref: {referenceCode})";
                await db.ExecAsync(
                    "UPDATE orders SET status='Reviewing', review_note=@note WHERE id=@id AND status IN ('Pending','Reviewing')",
                    ("@note", underpaidNote), ("@id", orderId));
                await Helpers.Audit(db, matchedOrder.S("user_id"), "sepay.webhook_underpaid", orderId,
                    $"order={orderCode}; received={transferAmount}; required={requiredAmount}; sepayId={sepayId}; ref={referenceCode}");
                await TryLogWebhookAsync(source, sepayId, gateway, transactionDate, accountNumber, transferType,
                    (int)Math.Clamp(transferAmount, 0, int.MaxValue), referenceCode, paymentCode, content, orderId, orderCode, "Underpaid", rawBody);
                return Results.Ok(new { success = true, status = "Underpaid", orderCode });
            }

            // Valid incoming payment >= requiredAmount: atomically mark order Paid, insert payment & extend subscription
            await using var tx = (SqliteTransaction)await db.BeginTransactionAsync();
            var freshOrder = await RowTx(db, tx, "SELECT * FROM orders WHERE id=@id", ("@id", orderId));
            if (freshOrder is null || freshOrder.S("status") == "Paid")
            {
                await tx.RollbackAsync();
                return Results.Ok(new { success = true, status = "AlreadyPaid", orderCode });
            }

            var adminIdObj = await ValueTx(db, tx, "SELECT id FROM users WHERE role='Admin' AND status='Active' ORDER BY created_at LIMIT 1");
            var confirmedBy = Convert.ToString(adminIdObj, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(confirmedBy))
                confirmedBy = freshOrder.S("user_id");

            var now = Helpers.Now();
            var bankRef = !string.IsNullOrWhiteSpace(sepayId) && !string.IsNullOrWhiteSpace(referenceCode)
                ? $"SEPAY-{sepayId}-{referenceCode}"
                : (!string.IsNullOrWhiteSpace(sepayId)
                    ? $"SEPAY-{sepayId}-{orderCode}"
                    : (!string.IsNullOrWhiteSpace(referenceCode) ? $"SEPAY-{referenceCode}-{orderCode}" : $"SEPAY-{orderCode}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"));
            if (bankRef.Length > 120) bankRef = bankRef[..120];

            var reviewNote = $"Tự động kích hoạt qua SePay ({gateway} · GD #{sepayId} · Ref: {referenceCode})";
            if (reviewNote.Length > 280) reviewNote = reviewNote[..280];

            var updated = await ExecTx(db, tx,
                "UPDATE orders SET status='Paid', paid_at=@now, reviewed_by=@admin, review_note=@note WHERE id=@id AND status <> 'Paid'",
                ("@now", now), ("@admin", confirmedBy), ("@note", reviewNote), ("@id", orderId));
            if (updated != 1)
            {
                await tx.RollbackAsync();
                return Results.Ok(new { success = true, status = "AlreadyPaid", orderCode });
            }

            await ExecTx(db, tx,
                "INSERT INTO payments(id,order_id,bank_reference,amount_vnd,confirmed_by,confirmed_at) VALUES(@pid,@id,@ref,@amount,@admin,@now)",
                ("@pid", Helpers.Id()), ("@id", orderId), ("@ref", bankRef),
                ("@amount", (int)Math.Clamp(transferAmount, requiredAmount, int.MaxValue)),
                ("@admin", confirmedBy), ("@now", now));

            var userId = freshOrder.S("user_id");
            var until = await PremiumUntilTx(db, tx, userId);
            var start = Helpers.IsPremium(until) ? DateTimeOffset.Parse(until, CultureInfo.InvariantCulture) : DateTimeOffset.UtcNow;
            var end = start.AddMonths(Math.Max(1, freshOrder.N("duration_months")));

            await ExecTx(db, tx,
                @"INSERT INTO subscriptions(id,user_id,starts_at,ends_at,source,order_id,granted_by,note,created_at)
                  VALUES(@sid,@uid,@start,@end,'Order',@order,@admin,@note,@now)",
                ("@sid", Helpers.Id()), ("@uid", userId), ("@start", start.ToString("O")), ("@end", end.ToString("O")),
                ("@order", orderId), ("@admin", confirmedBy), ("@note", reviewNote), ("@now", now));

            await ExecTx(db, tx,
                "UPDATE orders SET premium_start_at=@start, premium_end_at=@end WHERE id=@id",
                ("@start", start.ToString("O")), ("@end", end.ToString("O")), ("@id", orderId));

            await Helpers.Audit(db, confirmedBy, "sepay.webhook_confirm", orderId,
                $"order={orderCode}; user={userId}; amount={transferAmount}; bankRef={bankRef}; gateway={gateway}", tx);

            await tx.CommitAsync();

            await TryLogWebhookAsync(source, sepayId, gateway, transactionDate, accountNumber, transferType,
                (int)Math.Clamp(transferAmount, 0, int.MaxValue), referenceCode, paymentCode, content, orderId, orderCode, "Confirmed", rawBody);

            return Results.Ok(new
            {
                success = true,
                status = "Confirmed",
                orderCode,
                premiumUntil = end.ToString("O")
            });
        }
    }

    private static async Task<Dictionary<string, object?>?> FindMatchingOrderAsync(SqliteConnection db, string combinedText)
    {
        if (string.IsNullOrWhiteSpace(combinedText)) return null;

        // 1. Direct regex extraction for LKH... codes
        var matches = Regex.Matches(combinedText, @"LKH[A-Z0-9]{6,32}", RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            var candidate = m.Value.ToUpperInvariant();
            if (candidate.StartsWith("LKHAD", StringComparison.OrdinalIgnoreCase)) continue;
            var order = await db.FirstAsync(
                "SELECT * FROM orders WHERE UPPER(code)=@code AND order_type='Purchase' LIMIT 1",
                ("@code", candidate));
            if (order is not null) return order;
        }

        // 2. Fallback: strip spaces/punctuation from transfer content and compare against recent Purchase orders
        var normalized = Regex.Replace(combinedText, "[^A-Za-z0-9]", "").ToUpperInvariant();
        if (!normalized.Contains("LKH", StringComparison.Ordinal)) return null;

        var recentOrders = await db.ListAsync(
            "SELECT * FROM orders WHERE order_type='Purchase' ORDER BY created_at DESC LIMIT 300");
        foreach (var order in recentOrders)
        {
            var code = order.S("code").ToUpperInvariant();
            if (code.Length >= 8 && normalized.Contains(code, StringComparison.Ordinal))
                return order;
        }

        return null;
    }

    private static async Task TryLogWebhookAsync(
        Database source,
        string sepayTxId,
        string gateway,
        string transactionDate,
        string accountNumber,
        string transferType,
        int transferAmount,
        string referenceCode,
        string paymentCode,
        string content,
        string matchedOrderId,
        string matchedOrderCode,
        string processStatus,
        string rawJson)
    {
        try
        {
            await using var db = await source.OpenAsync();
            var clippedJson = rawJson.Length > 4000 ? rawJson[..4000] : rawJson;
            await db.ExecAsync(@"INSERT INTO sepay_webhook_logs
                (id,sepay_tx_id,gateway,transaction_date,account_number,transfer_type,transfer_amount,
                 reference_code,payment_code,content,matched_order_id,matched_order_code,process_status,raw_json,created_at)
                VALUES(@id,@tx,@gw,@dt,@acc,@type,@amt,@ref,@code,@cnt,@oid,@ocode,@st,@raw,@now)",
                ("@id", Helpers.Id()), ("@tx", sepayTxId), ("@gw", gateway), ("@dt", transactionDate),
                ("@acc", accountNumber), ("@type", transferType), ("@amt", transferAmount),
                ("@ref", referenceCode), ("@code", paymentCode), ("@cnt", content),
                ("@oid", matchedOrderId), ("@ocode", matchedOrderCode), ("@st", processStatus),
                ("@raw", clippedJson), ("@now", Helpers.Now()));
        }
        catch { }
    }

    private static string ReadStr(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return "";
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString()?.Trim() ?? "",
            JsonValueKind.Number => v.GetRawText().Trim(),
            _ => ""
        };
    }

    private static long ReadLong(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number)
        {
            if (v.TryGetInt64(out var n)) return n;
            if (v.TryGetDecimal(out var d)) return (long)Math.Round(d);
        }
        if (v.ValueKind == JsonValueKind.String &&
            long.TryParse(v.GetString()?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        return 0;
    }

    private static async Task<Dictionary<string, object?>?> RowTx(SqliteConnection db, SqliteTransaction tx, string sql, params (string, object?)[] args)
    {
        await using var cmd = DbExtensions.Cmd(db, sql, tx, args);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        var row = new Dictionary<string, object?>();
        for (var i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        return row;
    }

    private static async Task<object?> ValueTx(SqliteConnection db, SqliteTransaction tx, string sql, params (string, object?)[] args)
    {
        await using var cmd = DbExtensions.Cmd(db, sql, tx, args);
        return await cmd.ExecuteScalarAsync();
    }

    private static async Task<int> ExecTx(SqliteConnection db, SqliteTransaction tx, string sql, params (string, object?)[] args)
    {
        await using var cmd = DbExtensions.Cmd(db, sql, tx, args);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string> PremiumUntilTx(SqliteConnection db, SqliteTransaction tx, string id)
    {
        await using var cmd = DbExtensions.Cmd(db, "SELECT MAX(ends_at) FROM subscriptions WHERE user_id=@id", tx, ("@id", id));
        return Convert.ToString(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture) ?? "";
    }
}
