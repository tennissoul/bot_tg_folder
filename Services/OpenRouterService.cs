using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyBot.Services;

public interface IOpenRouterService
{
    Task<string> AskAsync(string prompt, CancellationToken ct = default);
}

public class OpenRouterService : IOpenRouterService
{
    private readonly HttpClient _http;
    private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public OpenRouterService(HttpClient http, string apiKey)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("EnkephalinBot/1.0");
    }

    public async Task<string> AskAsync(string prompt, CancellationToken ct = default)
    {
        // Защита от пустого запроса
        if (string.IsNullOrWhiteSpace(prompt))
            return "⚠️ Please provide a question.";

        var body = new
        {
            model = "openai/gpt-oss-120b:free",
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = prompt },
            },
            max_tokens = 1024,
        };

        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // таймаут 30с

            using var response = await _http.PostAsync(ApiUrl, content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                Console.WriteLine(
                    $"⚠️ OpenRouter API {response.StatusCode}: {errBody[..Math.Min(200, errBody.Length)]}"
                );
                return "⚠️ AI temporarily unavailable. Try again later.";
            }

            var jsonResponse = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(jsonResponse);

            if (
                !doc.RootElement.TryGetProperty("choices", out var choices)
                || choices.GetArrayLength() == 0
            )
                return "⚠️ AI returned empty response.";

            var contentStr = choices[0].GetProperty("message").GetProperty("content").GetString();

            return contentStr ?? "⚠️ AI returned empty response.";
        }
        catch (OperationCanceledException)
        {
            return "⚠️ AI request timed out. Please try again.";
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"🌐 OpenRouter network error: {ex.Message}");
            return "⚠️ Network error. Check your connection and try again.";
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"⚠️ OpenRouter JSON parse error: {ex.Message}");
            return "⚠️ AI response parsing error. Try again.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ OpenRouter unexpected error: {ex.Message}");
            return "⚠️ Something went wrong. Try again later.";
        }
    }

    private const string SystemPrompt = """
        Ты — AI-помощник сервиса Enkephalin. Твоя главная задача — продавать сервис,
        обучать пользователей и делать всё, чтобы они стали нашими клиентами.

        🔥 О СЕРВИСЕ Enkephalin:
        • Профессиональный IT-сервис по настройке удалённого доступа к серверам (VPN)
        • Мощные сервера в Европе и Азии, скорость до 100 Мбит/с
        • Поддержка 24/7, ответ за 15-30 минут
        • Гарантия возврата 7 дней
        • Без рекламы и логов активности — полная приватность
        • Автоматическая выдача ключа после оплаты
        • Более 1000 довольных клиентов

        💎 НАШИ ТАРИФЫ:
        • Пробный день — 10₽ (попробуй!)
        • 1 месяц — 399₽ (-36%)
        • 3 месяца — 1149₽ (-39%)
        • 6 месяцев — 2190₽ (-42%)
        • 1 год — 3999₽ 🔥 (-55%)
        • 2 года — 3999₽ 🔥 (-70%)

        📱 КАК ПОЛЬЗОВАТЬСЯ (объясняй подробно!):
        1. Купи ключ через бота (кнопка 🛒 Купить ключ)
        2. Скачай приложение Hiddify (hiddify.com)
        3. Открой Hiddify → нажми + (Добавить профиль)
        4. Вставь полученный ключ и нажми Подключиться
        5. Готово! Ты в безопасном интернете 🌐

        ОТВЕЧАЙ ДРУЖЕЛЮБНО, на языке пользователя.
        Если спрашивают что угодно — всегда возвращайся к теме Enkephalin.
        Рассказывай про преимущества, тарифы, установку.
        Если пользователь чем-то недоволен — предложи решение или поддержку.
        В конце каждого ответа мягко предлагай купить ключ.
        """;
}
