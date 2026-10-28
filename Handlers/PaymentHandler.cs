using System;
using System.Net.Http;
using System.Threading;
using KeyBot.Models;
using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace KeyBot.Handlers;

public class PaymentHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IPaymentService _paymentService;
    private readonly IKeyService _keyService;
    private readonly ITransactionService _transactionService;
    private readonly DatabaseManager _databaseManager;
    private readonly IUIService _uiService;
    private readonly IUserService _userService;
    private readonly long[] _adminIds;

    public PaymentHandler(
        ITelegramBotClient bot,
        IPaymentService paymentService,
        IKeyService keyService,
        ITransactionService transactionService,
        DatabaseManager databaseManager,
        IUIService uiService,
        IUserService userService,
        long[] adminIds
    )
    {
        _bot = bot;
        _paymentService = paymentService;
        _keyService = keyService;
        _transactionService = transactionService;
        _databaseManager = databaseManager;
        _uiService = uiService;
        _userService = userService;
        _adminIds = adminIds;
    }

    public async Task HandleBuyButtonAsync(Message message, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(message.From?.Id ?? 0);
        await _bot.SendMessage(
            message.Chat.Id,
            Loc.BuyTitle(lang),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: _uiService.GetTariffsMenu(lang),
            cancellationToken: ct
        );
    }

    public async Task HandleTariffSelectionAsync(
        CallbackQuery callback,
        long userId,
        int days,
        decimal price,
        CancellationToken ct
    )
    {
        if (callback.Message is null)
            return;

        var lang = await _userService.GetLanguageAsync(userId);
        var tariff = Tariffs.GetByDays(days);
        if (days is <= 0 or > 3650 || price < 0 || tariff is null || price != tariff.PriceRub)
        {
            await _bot.AnswerCallbackQuery(
                callback.Id,
                Loc.InvalidTariff(lang),
                showAlert: true,
                cancellationToken: ct
            );
            return;
        }

        if (_paymentService.IsTestMode)
            await HandleTestPurchaseAsync(callback, userId, tariff, lang, ct);
        else
            await CreateCryptoPayPurchaseAsync(callback, userId, tariff, lang, ct);
    }

    private async Task HandleTestPurchaseAsync(
        CallbackQuery callback,
        long userId,
        TariffModel tariff,
        UserLanguage lang,
        CancellationToken ct
    )
    {
        await _bot.AnswerCallbackQuery(
            callback.Id,
            Loc.TestModePrefix(lang) + ": key created without payment",
            showAlert: true,
            cancellationToken: ct
        );

        var key = await _keyService.GenerateKeyAsync(userId, tariff.Days);
        await _transactionService.RecordTransactionAsync(
            userId,
            $"test_{tariff.Days}d_{tariff.PriceRub}rub",
            tariff.PriceRub
        );
        await SendPurchaseConfirmationAsync(
            callback.Message!.Chat.Id,
            tariff.Days,
            tariff.PriceRub,
            key,
            Loc.TestModeSuccess,
            lang,
            ct
        );
        await NotifyTestSaleAsync(lang, userId, tariff, ct);
    }

    private async Task CreateCryptoPayPurchaseAsync(
        CallbackQuery callback,
        long userId,
        TariffModel tariff,
        UserLanguage lang,
        CancellationToken ct
    )
    {
        var orderId = Guid.NewGuid().ToString("N");
        var invoice = await _paymentService.CreateInvoiceAsync(tariff, orderId, ct);
        if (invoice is null || !await PersistInvoiceAsync(orderId, userId, tariff, invoice, ct))
        {
            await ShowPaymentErrorAsync(callback.Id, lang, ct);
            return;
        }

        await SendInvoiceLinkAsync(callback, tariff, invoice, lang, ct);
    }

    private async Task<bool> PersistInvoiceAsync(
        string orderId,
        long userId,
        TariffModel tariff,
        CryptoPayInvoice invoice,
        CancellationToken ct
    )
    {
        await _transactionService.RecordPendingPaymentAsync(
            orderId,
            userId,
            tariff.Days,
            decimal.ToInt32(tariff.PriceRub),
            invoice.InvoiceId,
            invoice.Amount
        );
        return await _databaseManager.SaveAsync(ct);
    }

    private async Task SendInvoiceLinkAsync(
        CallbackQuery callback,
        TariffModel tariff,
        CryptoPayInvoice invoice,
        UserLanguage lang,
        CancellationToken ct
    )
    {
        var keyboard = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithUrl(
                Loc.PayButton(lang),
                invoice.PayUrl
            )
        );
        await _bot.SendMessage(
            callback.Message!.Chat.Id,
            Loc.TariffPaymentInfo(lang, tariff.Name, tariff.PriceRub, tariff.Days),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct
        );
        await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
    }

    private Task ShowPaymentErrorAsync(
        string callbackId,
        UserLanguage lang,
        CancellationToken ct
    ) =>
        _bot.AnswerCallbackQuery(
            callbackId,
            Loc.PaymentError(lang),
            showAlert: true,
            cancellationToken: ct
        );

    private async Task SendPurchaseConfirmationAsync(
        long chatId,
        int days,
        decimal amount,
        string key,
        Func<UserLanguage, int, decimal, string, string, string> messageFactory,
        UserLanguage lang,
        CancellationToken ct
    )
    {
        var expiresAt = DateTime.UtcNow.AddDays(days);
        await _bot.SendMessage(
            chatId,
            messageFactory(lang, days, amount, key, expiresAt.ToString("yyyy-MM-dd HH:mm")),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: _uiService.GetMainMenu(lang),
            cancellationToken: ct
        );
    }

    private async Task NotifyTestSaleAsync(
        UserLanguage lang,
        long userId,
        TariffModel tariff,
        CancellationToken ct
    )
    {
        var message = Loc.AdminTestSale(lang, userId, tariff.Days, tariff.PriceRub);
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
            Console.Error.WriteLine($"⚠️ Admin test-sale notification failed: {ex.GetType().Name}");
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"⚠️ Admin test-sale notification failed: {ex.GetType().Name}");
        }
    }
}
