using System.Net.Http;
using System.Threading;
using KeyBot.Models;
using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace KeyBot.Handlers;

internal enum CryptoPayWebhookResult
{
    Processed,
    Ignored,
    Retry,
}

internal sealed class CryptoPayPaymentProcessor
{
    private readonly ITelegramBotClient _bot;
    private readonly IKeyService _keyService;
    private readonly ITransactionService _transactionService;
    private readonly DatabaseManager _databaseManager;
    private readonly IUIService _uiService;
    private readonly IUserService _userService;
    private readonly long[] _adminIds;
    private readonly SemaphoreSlim _paymentGate = new(1, 1);

    public CryptoPayPaymentProcessor(
        ITelegramBotClient bot,
        IKeyService keyService,
        ITransactionService transactionService,
        DatabaseManager databaseManager,
        IUIService uiService,
        IUserService userService,
        long[] adminIds
    )
    {
        _bot = bot;
        _keyService = keyService;
        _transactionService = transactionService;
        _databaseManager = databaseManager;
        _uiService = uiService;
        _userService = userService;
        _adminIds = adminIds;
    }

    public async Task<CryptoPayWebhookResult> HandlePaidInvoiceAsync(
        CryptoPayInvoiceUpdate update,
        CancellationToken ct
    )
    {
        await _paymentGate.WaitAsync(ct);
        try
        {
            return await ProcessPaymentAsync(update, ct);
        }
        finally
        {
            _paymentGate.Release();
        }
    }

    private async Task<CryptoPayWebhookResult> ProcessPaymentAsync(
        CryptoPayInvoiceUpdate update,
        CancellationToken ct
    )
    {
        var payment = await _transactionService.GetPaymentAsync(update.OrderId);
        if (payment is null || !MatchesPayment(payment, update))
            return CryptoPayWebhookResult.Ignored;
        if (payment.Status is not ("pending" or "completed"))
            return CryptoPayWebhookResult.Ignored;

        var wasNotified = payment.CustomerNotified;
        var key = await GetPaymentKeyAsync(payment, ct);
        if (key is null)
            return CryptoPayWebhookResult.Retry;

        if (!payment.CustomerNotified)
        {
            await NotifyCustomerAsync(payment, key, ct);
            if (!await _transactionService.MarkCustomerNotifiedAsync(payment.OrderId))
                return CryptoPayWebhookResult.Retry;
            if (!await _databaseManager.SaveAsync(ct))
                return CryptoPayWebhookResult.Retry;
        }

        if (!wasNotified)
            await NotifyAdminsAsync(payment, ct);
        return await _databaseManager.SaveAsync(ct)
            ? CryptoPayWebhookResult.Processed
            : CryptoPayWebhookResult.Retry;
    }

    private static bool MatchesPayment(PendingPayment? payment, CryptoPayInvoiceUpdate update) =>
        payment is not null
        && payment.InvoiceId == update.InvoiceId
        && payment.PaymentAmount == update.Amount
        && update.Asset == "USDT";

    private async Task<string?> GetPaymentKeyAsync(PendingPayment payment, CancellationToken ct)
    {
        if (payment.Status == "pending")
        {
            var key = await _keyService.GeneratePaidKeyAsync(
                payment.OrderId,
                payment.UserId,
                payment.Days
            );
            if (
                !await _transactionService.CompletePaymentAsync(
                    payment.OrderId,
                    payment.InvoiceId,
                    payment.PaymentAmount
                )
                || !await _databaseManager.SaveAsync(ct)
            )
                return null;
            return key;
        }

        return payment.Status == "completed"
            ? await _keyService.GetPaidKeyAsync(payment.OrderId)
            : null;
    }

    private async Task NotifyCustomerAsync(PendingPayment payment, string key, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(payment.UserId);
        await _bot.SendMessage(
            payment.UserId,
            Loc.PaymentSuccess(
                lang,
                payment.Days,
                payment.PriceRub,
                key,
                DateTime.UtcNow.AddDays(payment.Days).ToString("yyyy-MM-dd HH:mm")
            ),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: _uiService.GetMainMenu(lang),
            cancellationToken: ct
        );
    }

    private async Task NotifyAdminsAsync(PendingPayment payment, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(payment.UserId);
        var message = Loc.AdminNewSale(
            lang,
            payment.UserId,
            payment.Days,
            payment.PriceRub
        );
        foreach (var adminId in _adminIds)
            await SendAdminNotificationAsync(adminId, message, ct);
    }

    private async Task SendAdminNotificationAsync(
        long adminId,
        string message,
        CancellationToken ct
    )
    {
        try
        {
            await _bot.SendMessage(adminId, message, cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiRequestException ex)
        {
            Console.Error.WriteLine($"⚠️ Admin sale notification failed: {ex.GetType().Name}");
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"⚠️ Admin sale notification failed: {ex.GetType().Name}");
        }
    }
}
