using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KeyBot.Models;

namespace KeyBot.Services;

public interface IPaymentService
{
    Task<bool> ValidateInvoiceAsync(string invoicePayload, CancellationToken ct = default);
    Task<string> CreatePaymentLinkAsync(
        TariffModel tariff,
        long userId,
        CancellationToken ct = default
    );
    Task<bool> ProcessPaymentAsync(
        long userId,
        int days,
        decimal amount,
        CancellationToken ct = default
    );
}

public class CryptoPayService : IPaymentService
{
    private readonly string _apiToken;
    private readonly HttpClient _httpClient;
    private const string ApiBaseUrl = "https://pay.crypt.bot/api";
    private readonly bool _isTestMode;

    public CryptoPayService(string apiToken)
    {
        _apiToken = apiToken;
        _httpClient = new HttpClient();
        _isTestMode = string.IsNullOrEmpty(apiToken);

        if (_isTestMode)
        {
            Console.WriteLine("⚠️ CryptoPay: ТЕСТОВЫЙ РЕЖИМ (без реальных платежей)");
        }
    }

    public async Task<bool> ValidateInvoiceAsync(
        string invoicePayload,
        CancellationToken ct = default
    )
    {
        return !string.IsNullOrEmpty(invoicePayload);
    }

    public async Task<string> CreatePaymentLinkAsync(
        TariffModel tariff,
        long userId,
        CancellationToken ct = default
    )
    {
        if (_isTestMode || string.IsNullOrEmpty(_apiToken))
        {
            Console.WriteLine(
                $"🧪 ТЕСТ: Ключ для {userId} на {tariff.Days} дней за {tariff.PriceRub}₽"
            );
            return $"TEST_MODE_{userId}_{tariff.Days}_{tariff.PriceRub}";
        }

        try
        {
            decimal usdtAmount = tariff.PriceRub / 100m;
            usdtAmount = Math.Round(usdtAmount, 2);

            if (usdtAmount < 1m)
            {
                usdtAmount = 1m;
                Console.WriteLine($"⚠️ Минимальная сумма 1 USDT для тарифа {tariff.Days} дней");
            }

            Console.WriteLine($"💰 Создаём платеж: {usdtAmount} USDT для {userId}");

            // Ручной JSON (без сериализатора)
            string amountStr = usdtAmount.ToString(
                "0.00",
                System.Globalization.CultureInfo.InvariantCulture
            );
            string json =
                $"{{\"asset\":\"USDT\",\"amount\":\"{amountStr}\",\"description\":\"Key for {userId} - {tariff.Days} days\",\"hidden_message\":\"{userId}_{tariff.Days}_{tariff.PriceRub}\"}}";

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/createInvoice")
            {
                Content = content,
                Headers = { { "Crypto-Pay-API-Token", _apiToken } },
            };

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ CryptoPay API error: {response.StatusCode}");
                return $"TEST_MODE_{userId}_{tariff.Days}_{tariff.PriceRub}";
            }

            // Регуляркой вытаскиваем pay_url
            var match = Regex.Match(responseContent, "\"pay_url\":\"([^\"]+)\"");
            if (match.Success)
            {
                Console.WriteLine($"✅ Платёж создан: {usdtAmount} USDT");
                return match.Groups[1].Value;
            }

            return "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return $"TEST_MODE_{userId}_{tariff.Days}_{tariff.PriceRub}";
        }
    }

    public async Task<bool> ProcessPaymentAsync(
        long userId,
        int days,
        decimal amount,
        CancellationToken ct = default
    )
    {
        if (_isTestMode)
        {
            Console.WriteLine($"🧪 ТЕСТ: Платёж {userId} на {days}дн/{amount}₽");
            return true;
        }
        return true;
    }
}
