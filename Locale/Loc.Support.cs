namespace KeyBot.Services;

public static partial class Loc
{
    public static string SupportText(UserLanguage l) =>
        l == UserLanguage.English
            ? "📞 <b>User support</b>\n\nAverage response time: 15-30 minutes\n\nBefore contacting:\n• Check the /setup guide\n• Make sure you use the latest version"
            : "📞 <b>Поддержка пользователей</b>\n\nСреднее время ответа: 15-30 минут\n\nПеред обращением:\n• Проверьте инструкцию /setup\n• Убедитесь, что используете актуальную версию";

    public static string WriteSupportPrompt(UserLanguage l) =>
        l == UserLanguage.English
            ? "📝 Describe your issue in one message. An operator will reply shortly."
            : "📝 Опишите проблему одним сообщением. Оператор ответит вам в ближайшее время.";

    public static string SupportSent(UserLanguage l) =>
        l == UserLanguage.English
            ? "✅ Message sent! An operator will reply shortly."
            : "✅ Сообщение отправлено! Оператор ответит вам в ближайшее время.";

    public static string ProfileNotFound(UserLanguage l) =>
        l == UserLanguage.English
            ? "Profile not found. Use /start"
            : "Профиль не найден. Используйте /start";
}
