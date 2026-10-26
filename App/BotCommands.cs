namespace KeyBot.App;

/// <summary>Текстовые команды.</summary>
internal static class Commands
{
    public const string Start = "/start";
    public const string Buy = "🛒 Купить ключ";
    public const string Profile = "📊 Мой профиль";
    public const string Support = "❓ Поддержка";
    public const string Setup = "/setup";
    public const string Clear = "/clear";
    public const string Stats = "/stats";
    public const string Ban = "/ban";
    public const string Unban = "/unban";
    public const string Grant = "/grant";
    public const string Broadcast = "/broadcast";
    public const string Shutdown = "/shutdown";
    public const string Kill = "/kill";
    public const string Admin = "/admin";
    public const string Ai = "/ai";
    public const string Ask = "/ask";
}

/// <summary>Данные callback-кнопок.</summary>
internal static class CallbackData
{
    public const string BuyKey = "buy_key";
    public const string WriteSupport = "write_support";
    public const string BackToMain = "back_to_main";
    public const string Profile = "profile";
    public const string Support = "support";
    public const string About = "about";
    public const string Subscription = "subscription";
    public const string Referral = "referral";
    public const string ReferralActivate = "referral_activate";
    public const string LanguageMenu = "language_menu";
    public const string LangRu = "lang_ru";
    public const string LangEn = "lang_en";
    public const string AdminPanel = "admin_panel";
    public const string AdminGrant = "admin_grant";
    public const string AdminBack = "admin_back";
    public const string CancelSub = "cancel_sub";
    public const string CancelConfirmYes = "cancel_yes";
    public const string CancelConfirmNo = "cancel_no";
    public const string TariffPrefix = "buy_";
    public const string AdminStats = "admin_stats";
    public const string AdminBan = "admin_ban";
    public const string AdminUnban = "admin_unban";
    public const string AdminBroadcast = "admin_broadcast";
    public const string AdminShutdown = "admin_shutdown";
    public const string AdminRemoveSub = "admin_remove_sub";
}
