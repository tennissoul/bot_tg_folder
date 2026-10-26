namespace KeyBot.Services;

public static partial class Loc
{
    public static string Banned(UserLanguage l) =>
        l == UserLanguage.English ? "⛔️ Your account is blocked" : "⛔️ Ваш аккаунт заблокирован";

    public static string SetupInstruction(UserLanguage l) =>
        l == UserLanguage.English
            ? "📚 <b>Setup guide</b>\n\n1. Download the app\n2. Use your key to activate\n3. For details visit the link below\n\nNeed help? Contact support."
            : "📚 <b>Инструкция по настройке</b>\n\n1. Скачайте приложение\n2. Используйте ключ для активации\n3. Подробнее по ссылке ниже\n\nСложности? Обратитесь в поддержку.";

    public static string NoActiveSubscription(UserLanguage l) =>
        l == UserLanguage.English
            ? "📭 You have no active subscription.\n\nUse 🛒 Buy key to get access."
            : "📭 У вас нет активной подписки.\n\nИспользуйте 🛒 Купить ключ для получения доступа.";

    public static string SubscriptionInfo(
        UserLanguage l,
        string key,
        string expires,
        int daysLeft
    ) =>
        l == UserLanguage.English
            ? $"🤖 <b>Subscription management</b>\n\n🔑 Key: <code>{key}</code>\n⏰ Expires: {expires}\n📅 Days left: {daysLeft}"
            : $"🤖 <b>Управление подпиской</b>\n\n🔑 Ключ: <code>{key}</code>\n⏰ Действует до: {expires}\n📅 Осталось дней: {daysLeft}";

    public static string ClearedMessages(UserLanguage l, int count) =>
        l == UserLanguage.English
            ? $"🗑 Deleted {count} messages"
            : $"🗑 Удалено {count} сообщений";

    public static string ProfileText(
        UserLanguage l,
        long id,
        int purchases,
        decimal spent,
        string registered
    ) =>
        l == UserLanguage.English
            ? $"👤 <b>User Profile</b>\n\nID: <code>{id}</code>\nPurchases: {purchases}\nSpent: {spent} ₽\nRegistered: {registered}"
            : $"👤 <b>Профиль пользователя</b>\n\nID: <code>{id}</code>\nПокупок: {purchases}\nПотрачено: {spent} ₽\nРегистрация: {registered}";

    public static string ActiveKeys(UserLanguage l) =>
        l == UserLanguage.English ? "Active keys:" : "Активные ключи:";

    public static string Until(UserLanguage l) => l == UserLanguage.English ? "until" : "до";

    // ── Cancel subscription ──────────────────────────────────────────────────

    public static string BtnCancelSub(UserLanguage l) =>
        l == UserLanguage.English ? "⛔️ Cancel subscription" : "⛔️ Отменить подписку";

    public static string BtnConfirmYes(UserLanguage l) =>
        l == UserLanguage.English ? "✅ Yes" : "✅ Да";

    public static string BtnConfirmNo(UserLanguage l) =>
        l == UserLanguage.English ? "◀️ No" : "◀️ Нет";

    public static string CancelConfirm1(UserLanguage l) =>
        l == UserLanguage.English
            ? "❓ Are you sure you want to cancel your subscription?"
            : "❓ Вы точно хотите отменить подписку?";

    public static string CancelConfirm2(UserLanguage l) =>
        l == UserLanguage.English
            ? "⚠️ <b>You will lose access to your key without the possibility of recovery.</b>\n\nAre you sure?"
            : "⚠️ <b>Вы потеряете доступ к вашему ключу без возможности восстановления.</b>\n\nВы уверены?";

    public static string CancelFrozen(UserLanguage l) =>
        l == UserLanguage.English
            ? "❄️ <b>Subscription frozen</b>\n\nWe've frozen your subscription. It will be permanently deactivated in <b>30 days</b> so you can think it over.\n\nDuring this time, contact support to restore access."
            : "❄️ <b>Подписка заморожена</b>\n\nМы заморозили подписку. Она полностью сгорит через <b>30 дней</b>, чтобы вы могли подумать.\n\nЗа это время вы можете обратиться в поддержку и восстановить доступ.";

    public static string SubscriptionFrozenInfo(
        UserLanguage l,
        string key,
        string frozenUntil,
        string expires
    ) =>
        l == UserLanguage.English
            ? $"🤖 <b>Subscription frozen</b>\n\n🔑 Key: <code>{key}</code>\n❄️ Frozen until: {frozenUntil}\n⏰ Original expiry: {expires}\n\nContact support to restore."
            : $"🤖 <b>Подписка заморожена</b>\n\n🔑 Ключ: <code>{key}</code>\n❄️ Заморожена до: {frozenUntil}\n⏰ Истекала: {expires}\n\nОбратитесь в поддержку для восстановления.";
}
