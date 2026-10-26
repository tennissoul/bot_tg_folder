namespace KeyBot.Services;

public static partial class Loc
{
    public static string BtnBuy(UserLanguage l) =>
        l == UserLanguage.English ? "🛒 Buy key" : "🛒 Купить ключ";

    public static string BtnProfile(UserLanguage l) =>
        l == UserLanguage.English ? "📊 My profile" : "📊 Мой профиль";

    public static string BtnSupport(UserLanguage l) =>
        l == UserLanguage.English ? "❓ Support" : "❓ Поддержка";

    public static string BtnAbout(UserLanguage l) =>
        l == UserLanguage.English ? "🌟 About us" : "🌟 О нас";

    public static string BtnSubscription(UserLanguage l) =>
        l == UserLanguage.English ? "🤖 My subscription" : "🤖 Управление подпиской";

    public static string BtnReferral(UserLanguage l) =>
        l == UserLanguage.English ? "🤝 Referral program" : "🤝 Реферальная программа";

    public static string BtnLanguage(UserLanguage l) =>
        l == UserLanguage.English ? "💬 Language / Язык" : "💬 Язык / Language";

    public static string BtnBack(UserLanguage l) =>
        l == UserLanguage.English ? "◀️ Back" : "◀️ Назад";

    public static string BtnWriteSupport(UserLanguage l) =>
        l == UserLanguage.English ? "📝 Write to support" : "📝 Написать в поддержку";

    public static string MainMenu(UserLanguage l) =>
        l == UserLanguage.English ? "Main menu:" : "Главное меню:";

    public static string PayButton(UserLanguage l) =>
        l == UserLanguage.English ? "💳 Pay" : "💳 Оплатить";

    public static string BtnChannel(UserLanguage l) =>
        l == UserLanguage.English ? "📢 Our channel" : "📢 Наш канал";

    public static string BtnAdmin(UserLanguage l) =>
        l == UserLanguage.English ? "⚙️ Admin panel" : "⚙️ Админ панель";

    public static string BtnGrant(UserLanguage l) =>
        l == UserLanguage.English ? "🎁 Grant subscription" : "🎁 Выдать подписку";
}
