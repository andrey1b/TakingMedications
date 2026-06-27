using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TakingMedications.Services;

/// <summary>
/// Минимальный клиент Telegram Bot API для отправки напоминаний.
/// Совместим с Python med_telegram_setup.py.
/// </summary>
public static class TelegramService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static string ApiUrl(string token, string method)
        => $"https://api.telegram.org/bot{token}/{method}";

    /// <summary>Проверяет токен. Возвращает имя бота или null при ошибке.</summary>
    public static async Task<string?> ValidateTokenAsync(string token)
    {
        try
        {
            var resp = await _http.GetStringAsync(ApiUrl(token, "getMe"));
            using var doc = JsonDocument.Parse(resp);
            if (doc.RootElement.GetProperty("ok").GetBoolean())
            {
                var result = doc.RootElement.GetProperty("result");
                return result.GetProperty("username").GetString();
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Ждёт первого входящего сообщения (пользователь пишет /start боту).
    /// Возвращает chat_id или null при таймауте/ошибке.
    /// ct можно отменить из диалога.
    /// </summary>
    public static async Task<long?> WaitForFirstMessageAsync(string token, CancellationToken ct)
    {
        int offset = 0;
        try
        {
            // Сбросим очередь, получив текущий offset
            var flush = await _http.GetStringAsync(
                ApiUrl(token, "getUpdates?timeout=0&limit=1"), ct);
            using var fDoc = JsonDocument.Parse(flush);
            var fArr = fDoc.RootElement.GetProperty("result");
            if (fArr.GetArrayLength() > 0)
            {
                var last = fArr[fArr.GetArrayLength() - 1];
                offset = last.GetProperty("update_id").GetInt32() + 1;
            }

            // Долгий polling — ждём новое сообщение
            while (!ct.IsCancellationRequested)
            {
                var url  = ApiUrl(token, $"getUpdates?timeout=20&offset={offset}");
                var resp = await _http.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(resp);
                var arr  = doc.RootElement.GetProperty("result");
                foreach (var upd in arr.EnumerateArray())
                {
                    offset = upd.GetProperty("update_id").GetInt32() + 1;
                    if (upd.TryGetProperty("message", out var msg))
                        return msg.GetProperty("chat").GetProperty("id").GetInt64();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        return null;
    }

    /// <summary>Отправляет текстовое сообщение. Возвращает true при успехе.</summary>
    public static async Task<bool> SendMessageAsync(string token, long chatId, string text)
    {
        try
        {
            var body    = JsonSerializer.Serialize(new { chat_id = chatId, text });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp    = await _http.PostAsync(ApiUrl(token, "sendMessage"), content);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
