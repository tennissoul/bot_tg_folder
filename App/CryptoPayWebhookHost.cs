using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KeyBot.Handlers;
using KeyBot.Models;
using KeyBot.Services;
using Telegram.Bot.Exceptions;

namespace KeyBot.App;

internal sealed class CryptoPayWebhookHost : IAsyncDisposable
{
    private const int MaximumBodyBytes = 65_536;
    private const string WebhookPath = "/webhooks/cryptopay";
    private readonly HttpListener _listener = new();
    private readonly IPaymentService _paymentService;
    private readonly CryptoPayPaymentProcessor _paymentProcessor;
    private Task? _listenerTask;

    private CryptoPayWebhookHost(
        string listenUrl,
        IPaymentService paymentService,
        CryptoPayPaymentProcessor paymentProcessor
    )
    {
        _listener.Prefixes.Add(listenUrl.TrimEnd('/') + "/");
        _paymentService = paymentService;
        _paymentProcessor = paymentProcessor;
    }

    public static CryptoPayWebhookHost Create(
        string listenUrl,
        IPaymentService paymentService,
        CryptoPayPaymentProcessor paymentProcessor
    ) => new(listenUrl, paymentService, paymentProcessor);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        _listenerTask = ListenAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task Completion => _listenerTask ?? Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_listener.IsListening)
            _listener.Stop();
        if (_listenerTask is not null)
            await _listenerTask.WaitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _listener.Close();
        return ValueTask.CompletedTask;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException) when (!_listener.IsListening)
            {
                return;
            }

            await HandleRequestAsync(context, cancellationToken);
        }
    }

    private async Task HandleRequestAsync(
        HttpListenerContext context,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var validation = await ReadAuthenticatedBodyAsync(
                context.Request,
                cancellationToken
            );
            context.Response.StatusCode = (int)(
                validation.StatusCode == HttpStatusCode.OK && validation.Body is not null
                    ? await ProcessBodyAsync(validation.Body, cancellationToken)
                    : validation.StatusCode
            );
        }
        catch (JsonException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
        catch (ApiRequestException ex)
        {
            Console.Error.WriteLine($"⚠️ CryptoPay fulfilment notification failed: {ex.GetType().Name}");
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"⚠️ CryptoPay fulfilment request failed: {ex.GetType().Name}");
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (HttpListenerException ex)
            {
                Console.Error.WriteLine($"⚠️ Webhook response failed: {ex.GetType().Name}");
            }
        }
    }

    private async Task<(HttpStatusCode StatusCode, byte[]? Body)> ReadAuthenticatedBodyAsync(
        HttpListenerRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request.HttpMethod != "POST")
            return (HttpStatusCode.MethodNotAllowed, null);
        if (!string.Equals(request.Url?.AbsolutePath, WebhookPath, StringComparison.Ordinal))
            return (HttpStatusCode.NotFound, null);
        if (
            request.ContentType is null
            || !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
        )
            return (HttpStatusCode.UnsupportedMediaType, null);
        if (request.ContentLength64 > MaximumBodyBytes)
            return (HttpStatusCode.RequestEntityTooLarge, null);

        var signatures = request.Headers.GetValues("Crypto-Pay-API-Signature");
        if (signatures is not { Length: 1 })
            return (HttpStatusCode.Unauthorized, null);

        var body = await ReadBodyAsync(request, cancellationToken);
        if (body is null)
            return (HttpStatusCode.RequestEntityTooLarge, null);
        if (!_paymentService.ValidateWebhookSignature(body, signatures[0] ?? ""))
            return (HttpStatusCode.Unauthorized, null);
        return (HttpStatusCode.OK, body);
    }

    private async Task<HttpStatusCode> ProcessBodyAsync(
        byte[] body,
        CancellationToken cancellationToken
    )
    {
        var update = ParseInvoiceUpdate(body);
        if (update is null)
            return HttpStatusCode.OK;
        var result = await _paymentProcessor.HandlePaidInvoiceAsync(update, cancellationToken);
        return result == CryptoPayWebhookResult.Retry
            ? HttpStatusCode.ServiceUnavailable
            : HttpStatusCode.OK;
    }

    private static async Task<byte[]?> ReadBodyAsync(
        HttpListenerRequest request,
        CancellationToken cancellationToken
    )
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var bytesRead = await request.InputStream.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
                return buffer.ToArray();
            if (buffer.Length + bytesRead > MaximumBodyBytes)
                return null;

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static CryptoPayInvoiceUpdate? ParseInvoiceUpdate(byte[] body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (
            root.ValueKind != JsonValueKind.Object
            || !TryGetString(root, "update_type", out var updateType)
            || updateType != "invoice_paid"
        )
            return null;

        if (
            !root.TryGetProperty("payload", out var payload)
            || payload.ValueKind != JsonValueKind.Object
            || !TryGetString(payload, "payload", out var orderId)
            || !TryGetIdentifier(payload, "invoice_id", out var invoiceId)
            || !TryGetString(payload, "status", out var status)
            || status != "paid"
            || !TryGetString(payload, "asset", out var asset)
            || !TryGetDecimal(payload, "amount", out var amount)
        )
            throw new JsonException("Invalid Crypto Pay invoice update.");

        return new CryptoPayInvoiceUpdate(orderId, invoiceId, asset, amount);
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = "";
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value = property.GetString() ?? "");
    }

    private static bool TryGetIdentifier(
        JsonElement element,
        string propertyName,
        out string identifier
    )
    {
        identifier = "";
        if (!element.TryGetProperty(propertyName, out var property))
            return false;
        if (property.ValueKind == JsonValueKind.String)
            identifier = property.GetString() ?? "";
        else if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
            identifier = number.ToString(CultureInfo.InvariantCulture);

        return !string.IsNullOrWhiteSpace(identifier);
    }

    private static bool TryGetDecimal(
        JsonElement element,
        string propertyName,
        out decimal amount
    )
    {
        amount = 0;
        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        var value = property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null,
        };
        return decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount
        );
    }
}
