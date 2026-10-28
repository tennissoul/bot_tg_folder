using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KeyBot.Handlers;

public class SubscriptionHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IUserService _userService;
    private readonly IKeyService _keyService;
    private readonly IUIService _uiService;
    private readonly ConcurrentDictionary<long, int> _cancelStep = new(); // userId -> 1|2

    public SubscriptionHandler(
        ITelegramBotClient bot,
        IUserService userService,
        IKeyService keyService,
        IUIService uiService
    )
    {
        _bot = bot;
        _userService = userService;
        _keyService = keyService;
        _uiService = uiService;
    }

    public async Task HandleSubscriptionAsync(long chatId, long userId, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(userId);
        var keys = await _keyService.GetUserKeysAsync(userId);

        var activeKey = keys.FirstOrDefault(k => k.ExpiresAt > DateTime.UtcNow && !k.IsFrozen);

        if (activeKey == null)
        {
            // Показываем замороженную подписку если есть
            var frozenKey = keys.FirstOrDefault(k => k.IsFrozen);
            if (frozenKey != null)
            {
                var frozenAt = frozenKey.FrozenAt ?? DateTime.UtcNow;
                var frozenUntil = frozenAt.AddDays(30).ToString("yyyy-MM-dd HH:mm");
                await _bot.SendMessage(
                    chatId,
                    Loc.SubscriptionFrozenInfo(
                        lang,
                        frozenKey.Key,
                        frozenUntil,
                        frozenKey.ExpiresAt.ToString("yyyy-MM-dd HH:mm")
                    ),
                    parseMode: ParseMode.Html,
                    replyMarkup: _uiService.GetBackButton(lang),
                    cancellationToken: ct
                );
                return;
            }

            await _bot.SendMessage(
                chatId,
                Loc.NoActiveSubscription(lang),
                parseMode: ParseMode.Html,
                replyMarkup: _uiService.GetBackButton(lang),
                cancellationToken: ct
            );
            return;
        }

        int daysLeft = Math.Max(0, (int)(activeKey.ExpiresAt - DateTime.UtcNow).TotalDays);
        string expires = activeKey.ExpiresAt.ToString("yyyy-MM-dd HH:mm");

        await _bot.SendMessage(
            chatId,
            Loc.SubscriptionInfo(lang, activeKey.Key, expires, daysLeft),
            parseMode: ParseMode.Html,
            replyMarkup: _uiService.GetSubscriptionMenu(lang),
            cancellationToken: ct
        );
    }

    // ── Отмена подписки ──────────────────────────────────────────────────────

    public async Task HandleCancelAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Message == null)
            return;
        var lang = await _userService.GetLanguageAsync(cb.From.Id);
        _cancelStep[cb.From.Id] = 1;

        await _bot.EditMessageText(
            cb.Message.Chat.Id,
            cb.Message.MessageId,
            Loc.CancelConfirm1(lang),
            parseMode: ParseMode.Html,
            replyMarkup: _uiService.GetConfirmMenu(lang, "cancel_yes", "cancel_no"),
            cancellationToken: ct
        );
    }

    public async Task HandleCancelConfirmAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Message == null)
            return;
        var lang = await _userService.GetLanguageAsync(cb.From.Id);
        int step = _cancelStep.GetValueOrDefault(cb.From.Id, 0);

        if (step == 1)
        {
            // Require a separate confirmation before freezing the active subscription.
            _cancelStep[cb.From.Id] = 2;
            await _bot.EditMessageText(
                cb.Message.Chat.Id,
                cb.Message.MessageId,
                Loc.CancelConfirm2(lang),
                parseMode: ParseMode.Html,
                replyMarkup: _uiService.GetConfirmMenu(lang, "cancel_yes", "cancel_no"),
                cancellationToken: ct
            );
        }
        else if (step == 2)
        {
            _cancelStep.TryRemove(cb.From.Id, out _);
            await DoFreezeAsync(cb, lang, ct);
        }
        else
        {
            await _bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
        }
    }

    public async Task HandleCancelRejectAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Message == null)
            return;
        _cancelStep.TryRemove(cb.From.Id, out _);
        var lang = await _userService.GetLanguageAsync(cb.From.Id);

        // Возвращаемся к подписке
        await HandleSubscriptionAsync(cb.Message.Chat.Id, cb.From.Id, ct);
    }

    private async Task DoFreezeAsync(CallbackQuery cb, UserLanguage lang, CancellationToken ct)
    {
        if (cb.Message == null)
            return;
        var keys = await _keyService.GetUserKeysAsync(cb.From.Id);
        var activeKey = keys.FirstOrDefault(k => k.ExpiresAt > DateTime.UtcNow && !k.IsFrozen);

        if (activeKey == null)
        {
            await _bot.EditMessageText(
                cb.Message.Chat.Id,
                cb.Message.MessageId,
                Loc.NoActiveSubscription(lang),
                replyMarkup: _uiService.GetBackButton(lang),
                cancellationToken: ct
            );
            return;
        }

        await _keyService.FreezeKeyAsync(activeKey.Key, cb.From.Id);

        await _bot.EditMessageText(
            cb.Message.Chat.Id,
            cb.Message.MessageId,
            Loc.CancelFrozen(lang),
            parseMode: ParseMode.Html,
            replyMarkup: _uiService.GetBackButton(lang),
            cancellationToken: ct
        );
    }
}
