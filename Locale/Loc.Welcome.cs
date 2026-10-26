namespace KeyBot.Services;

public static partial class Loc
{
    public static string Welcome(UserLanguage l) =>
        l == UserLanguage.English
            ? "🔥 <b>Welcome to Enkephalin service</b>\n\nRemote server access setup service\n✅ 24/7 Support\n✅ No ads\n✅ 7-day refund guarantee\n\nChoose an action below 👇"
            : "🔥 <b>Добро пожаловать в сервис Enkephalin</b>\n\nIT-сервис по настройке удалённых доступов к серверам\n✅ Поддержка 24/7\n✅ Без рекламы\n✅ Гарантия возврата 7 дней\n\nВыберите действие в меню 👇";

    public static string AboutText(UserLanguage l) =>
        l == UserLanguage.English
            ? "🌟 <b>About Enkephalin</b>\n\n"
                + "Professional IT service for remote server access.\n\n"
                + "<b>Why choose us?</b>\n"
                + "• Servers in Europe and Asia\n"
                + "• Speed up to 100 Mbit/s\n"
                + "• 24/7 support, response in 15-30 min\n"
                + "• 7-day money-back guarantee\n"
                + "• No ads, no activity logs\n"
                + "• Automatic key delivery after payment\n\n"
                + "<b>Tariffs:</b>\n"
                + "• Trial day — 10₽\n• 1 month — 399₽\n• 3 months — 1149₽\n"
                + "• 6 months — 2190₽\n• 1 year — 3999₽\n• 2 years — 3999₽ 🔥\n\n"
                + "1000+ happy customers!"
            : "🌟 <b>О нас — Enkephalin</b>\n\n"
                + "Профессиональный IT-сервис по настройке удалённого доступа к серверам.\n\n"
                + "<b>Почему выбирают нас?</b>\n"
                + "• Сервера в Европе\n"
                + "• Скорость до 100 Мбит/с\n"
                + "• Поддержка 24/7, ответ за 15-30 минут\n"
                + "• Гарантия возврата 7 дней\n"
                + "• Без рекламы и логов активности\n"
                + "• Автоматическая выдача ключей после оплаты\n\n"
                + "<b>Тарифы:</b>\n"
                + "• Пробный день — 10₽\n• 1 месяц — 399₽\n• 3 месяца — 1149₽\n"
                + "• 6 месяцев — 2190₽\n• 1 год — 3999₽\n• 2 года — 3999₽ 🔥\n\n"
                + "1 довольный клиент! Наверное...";

    public static string ChooseLanguage(UserLanguage l) =>
        l == UserLanguage.English ? "💬 Choose your language:" : "💬 Выберите язык:";

    public static string LanguageChanged(UserLanguage l) =>
        l == UserLanguage.English ? "✅ Language set to English" : "✅ Язык изменён на Русский";

    public static string UseMenuButtons(UserLanguage l) =>
        l == UserLanguage.English ? "Please use the menu buttons 👇" : "Используйте кнопки меню 👇";

    public static string AiWelcome(UserLanguage l) =>
        l == UserLanguage.English
            ? "🤖 **Enkephalin AI — your virtual assistant!**\n\n"
                + "I can tell you about our service and help with any questions.\n\n"
                + "**What I can do:**\n"
                + "• Explain tariffs and prices\n"
                + "• Teach you to set up Hiddify\n"
                + "• Help with connection issues\n"
                + "• Recommend the best plan\n\n"
                + "**Just write:** `/ai your question`"
            : "🤖 **Enkephalin AI — ваш виртуальный помощник!**\n\n"
                + "Я расскажу о сервисе и помогу с любыми вопросами.\n\n"
                + "**Что я умею:**\n"
                + "• Расскажу о тарифах и ценах\n"
                + "• Научу настраивать Hiddify\n"
                + "• Помогу с подключением\n"
                + "• Порекомендую лучший тариф\n\n"
                + "**Просто напишите:** `/ai ваш вопрос`";
}
