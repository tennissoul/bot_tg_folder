namespace KeyBot.Services;

public static partial class Loc
{
    public static string BuyTitle(UserLanguage l) =>
        l == UserLanguage.English
            ? "💳 <b>Choose a plan:</b>"
            : "💳 <b>Выберите тарифный план:</b>";

    public static string InvalidTariff(UserLanguage l) =>
        l == UserLanguage.English ? "Invalid tariff" : "Неверный тариф";

    public static string PaymentError(UserLanguage l) =>
        l == UserLanguage.English
            ? "⚠️ Payment creation error. Try again later."
            : "⚠️ Ошибка создания платежа. Попробуйте позже.";

    public static string TariffPaymentInfo(
        UserLanguage l,
        string name,
        decimal priceRub,
        int days
    ) =>
        l == UserLanguage.English
            ? $"💳 <b>{name}</b>\n\nPrice: <b>{priceRub}₽</b>\nPeriod: {days} days\n\nClick the button below to pay:"
            : $"💳 <b>{name}</b>\n\nЦена: <b>{priceRub}₽</b>\nПериод: {days} дней\n\nНажмите кнопку ниже для оплаты:";

    public static string TestModePrefix(UserLanguage l) =>
        l == UserLanguage.English ? "🧪 TEST MODE" : "🧪 ТЕСТОВЫЙ РЕЖИМ";

    public static string TestModeSuccess(
        UserLanguage l,
        int days,
        decimal amount,
        string key,
        string expiresAt
    ) =>
        l == UserLanguage.English
            ? $"🧪 <b>TEST MODE</b>\n\n📦 Plan: {days} days\n💰 Amount: {amount} ₽ (not charged)\n🔑 Your test key:\n<code>{key}</code>\n\n⏰ Valid until: {expiresAt}\n\n⚠️ This is a test mode, no real payments!\n\n📱 Guide: /setup"
            : $"🧪 <b>ТЕСТОВЫЙ РЕЖИМ</b>\n\n📦 Тариф: {days} дней\n💰 Сумма: {amount} ₽ (не списана)\n🔑 Ваш тестовый ключ:\n<code>{key}</code>\n\n⏰ Действует до: {expiresAt}\n\n⚠️ Это тестовый режим, реальные платежи не производятся!\n\n📱 Инструкция: /setup";

    public static string PaymentSuccess(
        UserLanguage l,
        int days,
        decimal amount,
        string key,
        string expiresAt
    ) =>
        l == UserLanguage.English
            ? $"✅ <b>Payment successful!</b>\n\n📦 Plan: {days} days\n💰 Amount: {amount} ₽\n🔑 Your access key:\n<code>{key}</code>\n\n⏰ Valid until: {expiresAt}\n\n📱 Guide: /setup\n\n⚠️ <b>Do not share your key with anyone!</b>"
            : $"✅ <b>Оплата прошла успешно!</b>\n\n📦 Тариф: {days} дней\n💰 Сумма: {amount} ₽\n🔑 Ваш ключ доступа:\n<code>{key}</code>\n\n⏰ Действует до: {expiresAt}\n\n📱 Инструкция: /setup\n\n⚠️ <b>Не передавайте ключ третьим лицам!</b>";

    public static string AdminNewSale(UserLanguage _, long userId, int days, decimal amount) =>
        $"💰 <b>New sale!</b>\nUser: {userId}\nPlan: {days} d.\nAmount: {amount} ₽";

    public static string AdminTestSale(UserLanguage _, long userId, int days, decimal amount) =>
        $"🧪 <b>TEST sale!</b>\nUser: {userId}\nPlan: {days} d.\nAmount: {amount} ₽ (test)";

    public static string TariffName(UserLanguage l, int days) =>
        (l, days) switch
        {
            (UserLanguage.English, 1) => "Trial day",
            (UserLanguage.English, 30) => "1 month",
            (UserLanguage.English, 90) => "3 months",
            (UserLanguage.English, 180) => "🔥 6 months",
            (UserLanguage.English, 365) => "1 year",
            (UserLanguage.English, 730) => "💎 2 years",
            (_, 1) => "Пробный день",
            (_, 30) => "1 мес",
            (_, 90) => "3 мес",
            (_, 180) => "🔥 6 мес",
            (_, 365) => "1 год",
            (_, 730) => "💎 2 года",
            _ => $"{days} days",
        };
}
