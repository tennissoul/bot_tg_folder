namespace KeyBot.Services;

public static partial class Loc
{
    public static string NoPermission(UserLanguage l) =>
        l == UserLanguage.English ? "⛔️ Insufficient permissions" : "⛔️ Недостаточно прав";

    public static string StatsTitle(UserLanguage l, int users, decimal revenue, int activeKeys) =>
        l == UserLanguage.English
            ? $"📊 <b>Bot statistics</b>\n\n👥 Users: {users}\n💵 Revenue: {revenue} ₽\n🔑 Active keys: {activeKeys}"
            : $"📊 <b>Статистика бота</b>\n\n👥 Пользователей: {users}\n💵 Выручка: {revenue} ₽\n🔑 Активных ключей: {activeKeys}";

    public static string BanFormat(UserLanguage l) =>
        l == UserLanguage.English ? "Format: /ban <user_id>" : "Формат: /ban <user_id>";

    public static string UserBanned(UserLanguage l, long userId) =>
        l == UserLanguage.English
            ? $"✅ User {userId} blocked."
            : $"✅ Пользователь {userId} заблокирован.";

    public static string UserUnbanned(UserLanguage l, long userId) =>
        l == UserLanguage.English
            ? $"✅ User {userId} unblocked."
            : $"✅ Пользователь {userId} разблокирован.";

    public static string BroadcastReply(UserLanguage l) =>
        l == UserLanguage.English
            ? "Reply to the message to broadcast"
            : "Ответьте на сообщение для рассылки";

    public static string BroadcastComplete(UserLanguage l, int success, int failed) =>
        l == UserLanguage.English
            ? $"📤 Broadcast complete\n✅ Success: {success}\n❌ Failed: {failed}"
            : $"📤 Рассылка завершена\n✅ Успешно: {success}\n❌ Ошибок: {failed}";

    public static string ShutdownInitiated(UserLanguage l) =>
        l == UserLanguage.English
            ? "⚠️ Bot shutdown initiated. The process will complete shortly."
            : "⚠️ Инициирована остановка бота. Процесс завершится в ближайшее время.";

    public static string BotStoppedBy(UserLanguage _, long adminId) =>
        $"🔌 Bot stopped by admin {adminId}";

    public static string NewUserNotification(UserLanguage _, string? username, long userId) =>
        $"🆕 New user: @{username ?? "none"} (ID: {userId})";

    public static string AdminPanelTitle(UserLanguage l) =>
        l == UserLanguage.English
            ? "⚙️ <b>Admin panel</b>\n\nChoose an action:"
            : "⚙️ <b>Админ панель</b>\n\nВыберите действие:";

    public static string GrantPromptUser(UserLanguage l) =>
        l == UserLanguage.English
            ? "📝 Enter user ID to grant subscription:"
            : "📝 Введите ID пользователя для выдачи подписки:";

    public static string GrantPromptDays(UserLanguage l) =>
        l == UserLanguage.English ? "📝 Enter number of days:" : "📝 Введите количество дней:";

    public static string GrantSuccess(UserLanguage l, long userId, int days) =>
        l == UserLanguage.English
            ? $"✅ Subscription granted!\n👤 User: {userId}\n📅 Days: {days}"
            : $"✅ Подписка выдана!\n👤 Пользователь: {userId}\n📅 Дней: {days}";

    public static string GrantInvalidFormat(UserLanguage l) =>
        l == UserLanguage.English
            ? "❌ Invalid format. Use:\n<code>/grant user_id days</code>"
            : "❌ Неверный формат. Используйте:\n<code>/grant id_пользователя дней</code>";

    public static string GrantUserNotFound(UserLanguage l) =>
        l == UserLanguage.English ? "❌ User not found" : "❌ Пользователь не найден";

    // ── Admin panel buttons ──────────────────────────────────────────────────

    public static string BtnAdminStats(UserLanguage l) =>
        l == UserLanguage.English ? "📊 Statistics" : "📊 Статистика";

    public static string BtnAdminBan(UserLanguage l) =>
        l == UserLanguage.English ? "⛔️ Ban user" : "⛔️ Забанить";

    public static string BtnAdminUnban(UserLanguage l) =>
        l == UserLanguage.English ? "✅ Unban user" : "✅ Разбанить";

    public static string BtnAdminBroadcast(UserLanguage l) =>
        l == UserLanguage.English ? "📢 Broadcast" : "📢 Рассылка";

    public static string BtnAdminShutdown(UserLanguage l) =>
        l == UserLanguage.English ? "🔌 Shutdown bot" : "🔌 Остановить бота";

    public static string BanPromptUser(UserLanguage l) =>
        l == UserLanguage.English
            ? "⛔️ Enter user ID to ban:"
            : "⛔️ Введите ID пользователя для бана:";

    public static string UnbanPromptUser(UserLanguage l) =>
        l == UserLanguage.English
            ? "✅ Enter user ID to unban:"
            : "✅ Введите ID пользователя для разбана:";

    public static string ShutdownConfirm(UserLanguage l) =>
        l == UserLanguage.English
            ? "⚠️ Are you sure you want to shutdown the bot?"
            : "⚠️ Вы точно хотите остановить бота?";

    public static string BtnAdminRemoveSub(UserLanguage l) =>
        l == UserLanguage.English ? "🗑 Remove subscription" : "🗑 Удалить подписку";

    public static string RemoveSubPromptUser(UserLanguage l) =>
        l == UserLanguage.English
            ? "🗑 Enter user ID to remove subscription:"
            : "🗑 Введите ID пользователя для удаления подписки:";

    public static string RemoveSubSuccess(UserLanguage l, long userId) =>
        l == UserLanguage.English
            ? $"✅ Subscription removed for user {userId}."
            : $"✅ Подписка пользователя {userId} удалена.";

    public static string RemoveSubNotFound(UserLanguage l, long userId) =>
        l == UserLanguage.English
            ? $"❌ No active subscription found for user {userId}."
            : $"❌ Активная подписка у пользователя {userId} не найдена.";

    public static string BroadcastHint(UserLanguage l) =>
        l == UserLanguage.English
            ? "📢 <b>Broadcast</b>\n\nReply to a message with <code>/broadcast</code> to send it to all users."
            : "📢 <b>Рассылка</b>\n\nОтветьте на сообщение командой <code>/broadcast</code> чтобы разослать его всем пользователям.";
}
