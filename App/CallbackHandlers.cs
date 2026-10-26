using KeyBot.Handlers;
using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KeyBot.App;

/// <summary>Хендлеры для inline-кнопок (CallbackQuery).</summary>
internal class CallbackHandlers
{
    private readonly ITelegramBotClient _bot;
    private readonly MessageHandler _messageHandler;
    private readonly PaymentHandler _paymentHandler;
    private readonly AboutHandler _aboutHandler;
    private readonly SubscriptionHandler _subscriptionHandler;
    private readonly ReferralHandler _referralHandler;
    private readonly LanguageHandler _languageHandler;
    private readonly AdminHandler _adminHandler;
    private readonly IUserService _userService;
    private readonly IUIService _uiService;

    public CallbackHandlers(
        ITelegramBotClient bot,
        MessageHandler messageHandler,
        PaymentHandler paymentHandler,
        AboutHandler aboutHandler,
        SubscriptionHandler subscriptionHandler,
        ReferralHandler referralHandler,
        LanguageHandler languageHandler,
        AdminHandler adminHandler,
        IUserService userService,
        IUIService uiService
    )
    {
        _bot = bot;
        _messageHandler = messageHandler;
        _paymentHandler = paymentHandler;
        _aboutHandler = aboutHandler;
        _subscriptionHandler = subscriptionHandler;
        _referralHandler = referralHandler;
        _languageHandler = languageHandler;
        _adminHandler = adminHandler;
        _userService = userService;
        _uiService = uiService;
    }

    public Dictionary<
        string,
        Func<CallbackQuery, CancellationToken, Task>
    > BuildCallbackHandlers() =>
        new()
        {
            [CallbackData.BuyKey] = HandleBuyKeyAsync,
            [CallbackData.WriteSupport] = HandleWriteSupportAsync,
            [CallbackData.BackToMain] = HandleBackToMainAsync,
            [CallbackData.Profile] = HandleProfileAsync,
            [CallbackData.Support] = HandleSupportAsync,
            [CallbackData.About] = HandleAboutAsync,
            [CallbackData.Subscription] = HandleSubscriptionAsync,
            [CallbackData.Referral] = HandleReferralAsync,
            [CallbackData.ReferralActivate] = HandleReferralActivateAsync,
            [CallbackData.LanguageMenu] = HandleLanguageMenuAsync,
            [CallbackData.LangRu] = HandleLangRuAsync,
            [CallbackData.LangEn] = HandleLangEnAsync,
            [CallbackData.AdminPanel] = HandleAdminPanelAsync,
            [CallbackData.AdminGrant] = HandleAdminGrantAsync,
            [CallbackData.AdminStats] = HandleAdminStatsAsync,
            [CallbackData.AdminBan] = HandleAdminBanAsync,
            [CallbackData.AdminUnban] = HandleAdminUnbanAsync,
            [CallbackData.AdminBroadcast] = HandleAdminBroadcastAsync,
            [CallbackData.AdminRemoveSub] = HandleAdminRemoveSubAsync,
            [CallbackData.AdminShutdown] = HandleAdminShutdownAsync,
            [CallbackData.CancelSub] = HandleCancelSubAsync,
            [CallbackData.CancelConfirmYes] = HandleCancelConfirmYesAsync,
            [CallbackData.CancelConfirmNo] = HandleCancelConfirmNoAsync,
        };

    // ── Handlers ─────────────────────────────────────────────────────────────

    private async Task HandleBuyKeyAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Message == null)
            return;
        var lang = await _userService.GetLanguageAsync(cb.From.Id);
        await _bot.SendMessage(
            cb.Message.Chat.Id,
            Loc.BuyTitle(lang),
            parseMode: ParseMode.Html,
            replyMarkup: _uiService.GetTariffsMenu(lang),
            cancellationToken: ct
        );
    }

    private Task HandleWriteSupportAsync(CallbackQuery cb, CancellationToken ct) =>
        _messageHandler.HandleWriteSupportAsync(cb.From.Id, cb.Message?.Chat?.Id ?? 0, ct);

    private async Task HandleBackToMainAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Message == null)
            return;
        var lang = await _userService.GetLanguageAsync(cb.From.Id);
        await _bot.DeleteMessage(cb.Message.Chat.Id, cb.Message.MessageId, ct);
        await _bot.SendMessage(
            cb.Message.Chat.Id,
            Loc.MainMenu(lang),
            replyMarkup: _uiService.GetMainMenu(lang),
            cancellationToken: ct
        );
    }

    private Task HandleProfileAsync(CallbackQuery cb, CancellationToken ct) =>
        _messageHandler.HandleProfileAsync(
            new Message { From = cb.From, Chat = cb.Message?.Chat ?? new Chat() },
            ct
        );

    private Task HandleSupportAsync(CallbackQuery cb, CancellationToken ct) =>
        _messageHandler.HandleSupportAsync(
            new Message { From = cb.From, Chat = cb.Message?.Chat ?? new Chat() },
            ct
        );

    private Task HandleAboutAsync(CallbackQuery cb, CancellationToken ct) =>
        _aboutHandler.HandleAboutAsync(cb.Message?.Chat.Id ?? cb.From.Id, cb.From.Id, ct);

    private Task HandleSubscriptionAsync(CallbackQuery cb, CancellationToken ct) =>
        _subscriptionHandler.HandleSubscriptionAsync(
            cb.Message?.Chat.Id ?? cb.From.Id,
            cb.From.Id,
            ct
        );

    private Task HandleReferralAsync(CallbackQuery cb, CancellationToken ct) =>
        _referralHandler.HandleReferralMenuAsync(cb.Message?.Chat.Id ?? cb.From.Id, cb.From.Id, ct);

    private Task HandleReferralActivateAsync(CallbackQuery cb, CancellationToken ct) =>
        _referralHandler.HandleActivateBonusAsync(
            cb.Message?.Chat.Id ?? cb.From.Id,
            cb.From.Id,
            ct
        );

    private Task HandleLanguageMenuAsync(CallbackQuery cb, CancellationToken ct) =>
        _languageHandler.HandleLanguageMenuAsync(cb.Message?.Chat.Id ?? cb.From.Id, cb.From.Id, ct);

    private Task HandleLangRuAsync(CallbackQuery cb, CancellationToken ct) =>
        _languageHandler.HandleSetLanguageAsync(
            cb.Message?.Chat.Id ?? cb.From.Id,
            cb.From.Id,
            "ru",
            ct
        );

    private Task HandleLangEnAsync(CallbackQuery cb, CancellationToken ct) =>
        _languageHandler.HandleSetLanguageAsync(
            cb.Message?.Chat.Id ?? cb.From.Id,
            cb.From.Id,
            "en",
            ct
        );

    private Task HandleAdminPanelAsync(CallbackQuery cb, CancellationToken ct) =>
        _adminHandler.ShowAdminPanelAsync(cb.Message?.Chat.Id ?? cb.From.Id, cb.From.Id, ct);

    private Task HandleAdminGrantAsync(CallbackQuery cb, CancellationToken ct) =>
        _adminHandler.HandleGrantCallbackAsync(cb, ct);

    private Task HandleAdminStatsAsync(CallbackQuery cb, CancellationToken ct) =>
        _adminHandler.HandleStatsCallbackAsync(cb, ct);

    private Task HandleAdminBanAsync(CallbackQuery cb, CancellationToken ct) =>
        _adminHandler.HandleBanCallbackAsync(cb, ct);

    private Task HandleAdminUnbanAsync(CallbackQuery cb, CancellationToken ct) =>
        _adminHandler.HandleUnbanCallbackAsync(cb, ct);

    private Task HandleAdminBroadcastAsync(CallbackQuery cb, CancellationToken ct) =>
        _adminHandler.HandleBroadcastCallbackAsync(cb, ct);

    private Task HandleAdminRemoveSubAsync(CallbackQuery cb, CancellationToken ct) =>
        _adminHandler.HandleRemoveSubCallbackAsync(cb, ct);

    private Task HandleAdminShutdownAsync(CallbackQuery cb, CancellationToken ct) =>
        _adminHandler.HandleShutdownCallbackAsync(cb, ct);

    private Task HandleCancelSubAsync(CallbackQuery cb, CancellationToken ct) =>
        _subscriptionHandler.HandleCancelAsync(cb, ct);

    private Task HandleCancelConfirmYesAsync(CallbackQuery cb, CancellationToken ct) =>
        _subscriptionHandler.HandleCancelConfirmAsync(cb, ct);

    private Task HandleCancelConfirmNoAsync(CallbackQuery cb, CancellationToken ct) =>
        _subscriptionHandler.HandleCancelRejectAsync(cb, ct);

    /// <summary>Разбирает buy_30_399 и запускает платёж.</summary>
    public Task HandleTariffPurchaseAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(cb.Data))
            return Task.CompletedTask;
        var parts = cb.Data.Split('_');
        if (
            parts.Length == 3
            && int.TryParse(parts[1], out int days)
            && decimal.TryParse(
                parts[2],
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal price
            )
        )
        {
            return _paymentHandler.HandleTariffSelectionAsync(cb, cb.From.Id, days, price, ct);
        }
        return Task.CompletedTask;
    }
}
