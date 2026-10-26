using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace KeyBot.Handlers;

public class ReferralHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IUserService _userService;
    private readonly IReferralService _referralService;
    private readonly IUIService _uiService;
    private readonly string _botUsername;
    private readonly Func<Task<List<long>>>? _getAllUserIds;

    public ReferralHandler(
        ITelegramBotClient bot,
        IUserService userService,
        IReferralService referralService,
        IUIService uiService,
        string botUsername,
        Func<Task<List<long>>>? getAllUserIds = null
    )
    {
        _bot = bot;
        _userService = userService;
        _referralService = referralService;
        _uiService = uiService;
        _botUsername = botUsername;
        _getAllUserIds = getAllUserIds;
    }

    public async Task HandleReferralMenuAsync(long chatId, long userId, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(userId);
        var stats = await _referralService.GetStatsAsync(userId);
        var code = _referralService.GetRefCode(userId);

        string botLink = $"https://t.me/{_botUsername}?start={code}";

        string text =
            lang == UserLanguage.English
                ? BuildReferralTextEn(botLink, code, stats)
                : BuildReferralTextRu(botLink, code, stats);

        var keyboard = BuildKeyboard(lang, stats.BalanceDays);

        await _bot.SendMessage(
            chatId,
            text,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct
        );
    }

    public async Task HandleActivateBonusAsync(long chatId, long userId, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(userId);
        int days = await _referralService.ActivateBonusDaysAsync(userId);

        string msg =
            lang == UserLanguage.English
                ? (days > 0 ? $"✅ Activated {days} bonus days!" : "📭 No bonus days to activate.")
                : (
                    days > 0
                        ? $"✅ Активировано {days} бонусных дней!"
                        : "📭 Нет бонусных дней для активации."
                );

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);

        // Перерисовываем меню
        await HandleReferralMenuAsync(chatId, userId, ct);
    }

    // ── Обработка старта по реферальной ссылке (/start <code>) ───────────────

    public async Task TryRegisterReferralAsync(
        long newUserId,
        string startParam,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(startParam))
            return;

        // Ищем владельца кода среди пользователей
        var allUserIds = await GetAllUserIdsAsync();
        foreach (var candidateId in allUserIds)
        {
            if (_referralService.GetRefCode(candidateId) == startParam)
            {
                await _referralService.RegisterReferralAsync(candidateId, newUserId);
                return;
            }
        }
    }

    // ── Приватные ─────────────────────────────────────────────────────────────

    private Task<List<long>> GetAllUserIdsAsync() =>
        _getAllUserIds?.Invoke() ?? Task.FromResult(new List<long>());

    private static string BuildReferralTextRu(string botLink, string code, ReferralStats s) =>
        "🤝 <b>Реферальная программа Enkephalin</b>\n\n"
        + "Приводи друзей и получай бонусы! Просто отправь ссылку, и после покупки друга "
        + "тебе автоматически начислятся бесплатные дни, а другу — 3 дня в подарок!\n\n"
        + $"🤖 <b>Твоя реферальная ссылка:</b>\n<code>{botLink}</code>\n"
        + "<i>(нажми, чтобы скопировать)</i>\n\n"
        + $"🎁 <b>Баланс:</b> {s.BalanceDays} дней\n\n"
        + "<b>📊 Твоя статистика:</b>\n"
        + $"• Зарегистрировалось по ссылке: {s.TotalReferred}\n"
        + $"• Оплатили подписку: {s.TotalPurchased}\n"
        + $"• Ожидает начисления: {s.PendingDays} дней\n"
        + $"• Всего заработано: {s.TotalEarnedDays} дней\n\n"
        + "📃 <b>Условия:</b>\n"
        + "• Бонусы начисляются за каждую покупку от 1 месяца;\n"
        + "• Реферал закрепляется за тобой навсегда;\n"
        + "• Бесплатные дни можно активировать в любой момент;\n"
        + "• Рефералу начисляются 3 бонусных дня при первой покупке;\n"
        + "• Бонусы начисляются через 48 часов после покупки.";

    private static string BuildReferralTextEn(string botLink, string code, ReferralStats s) =>
        "🤝 <b>Enkephalin Referral Program</b>\n\n"
        + "Invite friends and earn bonuses! Share your link, and after your friend's purchase "
        + "you get free days automatically, and your friend gets 3 days as a gift!\n\n"
        + $"🤖 <b>Your referral link:</b>\n<code>{botLink}</code>\n"
        + "<i>(tap to copy)</i>\n\n"
        + $"🎁 <b>Balance:</b> {s.BalanceDays} days\n\n"
        + "<b>📊 Your stats:</b>\n"
        + $"• Registered via link: {s.TotalReferred}\n"
        + $"• Purchased subscription: {s.TotalPurchased}\n"
        + $"• Pending: {s.PendingDays} days\n"
        + $"• Total earned: {s.TotalEarnedDays} days\n\n"
        + "📃 <b>Terms:</b>\n"
        + "• Bonuses for every purchase of 1+ month;\n"
        + "• Referral is tied to you permanently;\n"
        + "• Bonus days can be activated anytime;\n"
        + "• Referee gets 3 bonus days on first purchase;\n"
        + "• Bonuses are credited 48h after purchase.";

    private InlineKeyboardMarkup BuildKeyboard(UserLanguage lang, int balanceDays)
    {
        var rows = new List<InlineKeyboardButton[]>();

        if (balanceDays > 0)
        {
            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        lang == UserLanguage.English
                            ? $"🎁 Activate {balanceDays} days"
                            : $"🎁 Активировать {balanceDays} дней",
                        "referral_activate"
                    ),
                }
            );
        }

        rows.Add(
            new[] { InlineKeyboardButton.WithCallbackData(Loc.BtnBack(lang), "back_to_main") }
        );
        return new InlineKeyboardMarkup(rows);
    }
}
