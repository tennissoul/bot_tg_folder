using KeyBot.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace KeyBot.Services;

public interface IUIService
{
    InlineKeyboardMarkup GetMainMenu(UserLanguage lang = UserLanguage.Russian);
    InlineKeyboardMarkup GetTariffsMenu(UserLanguage lang = UserLanguage.Russian);
    InlineKeyboardMarkup GetSupportMenu(UserLanguage lang = UserLanguage.Russian);
    InlineKeyboardMarkup GetBackButton(UserLanguage lang = UserLanguage.Russian);
    InlineKeyboardMarkup GetAdminMenu(UserLanguage lang = UserLanguage.Russian);
    InlineKeyboardMarkup GetSubscriptionMenu(UserLanguage lang = UserLanguage.Russian);
    InlineKeyboardMarkup GetConfirmMenu(UserLanguage lang, string yesData, string noData);
}

public class UIService : IUIService
{
    // Кэш клавиатур — уменьшает GC-давление (создаём один раз на язык)
    private readonly Dictionary<UserLanguage, InlineKeyboardMarkup> _mainMenuCache = new(2);
    private readonly Dictionary<UserLanguage, InlineKeyboardMarkup> _supportMenuCache = new(2);
    private readonly Dictionary<UserLanguage, InlineKeyboardMarkup> _backButtonCache = new(2);
    private readonly Dictionary<UserLanguage, InlineKeyboardMarkup> _adminMenuCache = new(2);

    public InlineKeyboardMarkup GetMainMenu(UserLanguage lang = UserLanguage.Russian)
    {
        if (_mainMenuCache.TryGetValue(lang, out var cached))
            return cached;
        return _mainMenuCache[lang] = new InlineKeyboardMarkup([
            [InlineKeyboardButton.WithCallbackData(Loc.BtnBuy(lang), "buy_key")],
            [InlineKeyboardButton.WithCallbackData(Loc.BtnSubscription(lang), "subscription")],
            [
                InlineKeyboardButton.WithCallbackData(Loc.BtnProfile(lang), "profile"),
                InlineKeyboardButton.WithCallbackData(Loc.BtnSupport(lang), "support"),
            ],
            [
                InlineKeyboardButton.WithCallbackData(Loc.BtnReferral(lang), "referral"),
                InlineKeyboardButton.WithCallbackData(Loc.BtnAbout(lang), "about"),
            ],
            [InlineKeyboardButton.WithUrl(Loc.BtnChannel(lang), "https://t.me/EnkephalinVPN")],
            [InlineKeyboardButton.WithCallbackData(Loc.BtnLanguage(lang), "language_menu")],
            [InlineKeyboardButton.WithCallbackData(Loc.BtnAdmin(lang), "admin_panel")],
        ]);
    }

    public InlineKeyboardMarkup GetTariffsMenu(UserLanguage lang = UserLanguage.Russian)
    {
        var rows = new List<InlineKeyboardButton[]>(Tariffs.Available.Length + 1);
        foreach (var t in Tariffs.Available)
            rows.Add([
                InlineKeyboardButton.WithCallbackData(
                    $"{t.Name} - {t.PriceRub}₽",
                    $"buy_{t.Days}_{t.PriceRub}"
                ),
            ]);
        rows.Add([InlineKeyboardButton.WithCallbackData(Loc.BtnBack(lang), "back_to_main")]);
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup GetSupportMenu(UserLanguage lang = UserLanguage.Russian)
    {
        if (_supportMenuCache.TryGetValue(lang, out var cached))
            return cached;
        return _supportMenuCache[lang] = new InlineKeyboardMarkup([
            [InlineKeyboardButton.WithCallbackData(Loc.BtnWriteSupport(lang), "write_support")],
            [InlineKeyboardButton.WithCallbackData(Loc.BtnBack(lang), "back_to_main")],
        ]);
    }

    public InlineKeyboardMarkup GetBackButton(UserLanguage lang = UserLanguage.Russian)
    {
        if (_backButtonCache.TryGetValue(lang, out var cached))
            return cached;
        return _backButtonCache[lang] = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData(Loc.BtnBack(lang), "back_to_main")
        );
    }

    public InlineKeyboardMarkup GetAdminMenu(UserLanguage lang = UserLanguage.Russian)
    {
        if (_adminMenuCache.TryGetValue(lang, out var cached))
            return cached;
        return _adminMenuCache[lang] = new InlineKeyboardMarkup([
            [InlineKeyboardButton.WithCallbackData(Loc.BtnAdminStats(lang), "admin_stats")],
            [InlineKeyboardButton.WithCallbackData(Loc.BtnGrant(lang), "admin_grant")],
            [
                InlineKeyboardButton.WithCallbackData(Loc.BtnAdminBan(lang), "admin_ban"),
                InlineKeyboardButton.WithCallbackData(Loc.BtnAdminUnban(lang), "admin_unban"),
            ],
            [InlineKeyboardButton.WithCallbackData(Loc.BtnAdminBroadcast(lang), "admin_broadcast")],
            [InlineKeyboardButton.WithCallbackData(Loc.BtnAdminRemoveSub(lang), "admin_remove_sub")],
            [InlineKeyboardButton.WithCallbackData(Loc.BtnAdminShutdown(lang), "admin_shutdown")],
            [InlineKeyboardButton.WithCallbackData(Loc.BtnBack(lang), "back_to_main")],
        ]);
    }

    public InlineKeyboardMarkup GetSubscriptionMenu(UserLanguage lang = UserLanguage.Russian)
    {
        return new InlineKeyboardMarkup([
            [InlineKeyboardButton.WithCallbackData(Loc.BtnCancelSub(lang), "cancel_sub")],
            [InlineKeyboardButton.WithCallbackData(Loc.BtnBack(lang), "back_to_main")],
        ]);
    }

    public InlineKeyboardMarkup GetConfirmMenu(UserLanguage lang, string yesData, string noData)
    {
        return new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData(Loc.BtnConfirmYes(lang), yesData),
                InlineKeyboardButton.WithCallbackData(Loc.BtnConfirmNo(lang), noData),
            ],
        ]);
    }
}
