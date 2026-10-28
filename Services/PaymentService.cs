using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using KeyBot.Models;

namespace KeyBot.Services;

public interface IPaymentService
{
    bool IsTestMode { get; }
    bool ValidateWebhookSignature(ReadOnlySpan<byte> body, string signature);
    Task<CryptoPayInvoice?> CreateInvoiceAsync(
        TariffModel tariff,
        string orderId,
        CancellationToken ct = default
    );
}

public class CryptoPayService : IPaymentService
{
    private readonly string _apiToken;
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;
    private const string ApiBaseUrl = "https://pay.crypt.bot/api";
    public bool IsTestMode { get; }

    public CryptoPayService(
        string apiToken,
        HttpClient httpClient,
        bool allowTestPayments,
        string webhookUrl
    )
    {
        _apiToken = apiToken;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _webhookUrl = webhookUrl;
        IsTestMode = allowTestPayments && string.IsNullOrEmpty(apiToken);

        if (IsTestMode)
        {
            Console.WriteLine("⚠️ CryptoPay: явно включён тестовый режим без реальных платежей");
        }
    }

    public bool ValidateWebhookSignature(ReadOnlySpan<byte> body, string signature)
    {
        if (string.IsNullOrEmpty(_apiToken) || signature.Length != 64)
            return false;

        byte[] receivedSignature;
        try
        {
            receivedSignature = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }
        if (receivedSignature.Length != 32)
            return false;

        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(_apiToken));
        var expectedSignature = HMACSHA256.HashData(secret, body);
        return CryptographicOperations.FixedTimeEquals(expectedSignature, receivedSignature);
    }

    public async Task<CryptoPayInvoice?> CreateInvoiceAsync(
        TariffModel tariff,
        string orderId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrEmpty(_apiToken))
        {
            Console.WriteLine("❌ CryptoPay не настроен; ссылка на оплату не создана");
            return null;
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

            Console.WriteLine($"💰 Создаём платёж: {usdtAmount} USDT");

            // Ручной JSON (без сериализатора)
            string amountStr = usdtAmount.ToString(
                "0.00",
                CultureInfo.InvariantCulture
            );
            string json = JsonSerializer.Serialize(
                new
                {
                    asset = "USDT",
                    amount = amountStr,
                    description = $"VPN subscription - {tariff.Days} days",
                    payload = orderId,
                    webhook_url = _webhookUrl,
                }
            );

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{ApiBaseUrl}/createInvoice"
            )
            {
                Content = content,
                Headers = { { "Crypto-Pay-API-Token", _apiToken } },
            };

            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            );

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ CryptoPay API error: {response.StatusCode}");
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(ct);
            using var responseDocument = await JsonDocument.ParseAsync(
                responseStream,
                cancellationToken: ct
            );
            var root = responseDocument.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            if (
                root.TryGetProperty("ok", out var success)
                && success.ValueKind == JsonValueKind.True
                && root.TryGetProperty("result", out var result)
                && result.ValueKind == JsonValueKind.Object
                && result.TryGetProperty("invoice_id", out var invoiceIdValue)
                && result.TryGetProperty("pay_url", out var payUrl)
                && result.TryGetProperty("asset", out var asset)
                && asset.ValueKind == JsonValueKind.String
                && asset.GetString() == "USDT"
                && result.TryGetProperty("amount", out var invoiceAmountValue)
                && TryGetAmount(invoiceAmountValue, out var invoiceAmount)
                && invoiceAmount == usdtAmount
                && TryGetInvoiceId(invoiceIdValue, out var invoiceId)
                && payUrl.ValueKind == JsonValueKind.String
                && Uri.TryCreate(payUrl.GetString(), UriKind.Absolute, out var paymentUri)
                && paymentUri.Scheme == Uri.UriSchemeHttps
            )
            {
                Console.WriteLine($"✅ Платёж создан: {usdtAmount} USDT");
                return new CryptoPayInvoice(invoiceId, paymentUri.AbsoluteUri, usdtAmount);
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"❌ CryptoPay network error: {ex.GetType().Name}");
            return null;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"❌ CryptoPay response parse error: {ex.GetType().Name}");
            return null;
        }
    }

    private static bool TryGetInvoiceId(JsonElement value, out string invoiceId)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            invoiceId = value.GetString() ?? "";
            return !string.IsNullOrWhiteSpace(invoiceId);
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var numericId))
        {
            invoiceId = numericId.ToString(CultureInfo.InvariantCulture);
            return numericId > 0;
        }

        invoiceId = "";
        return false;
    }

    private static bool TryGetAmount(JsonElement value, out decimal amount)
    {
        var amountText = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        return decimal.TryParse(
            amountText,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount
        );
    }
}
