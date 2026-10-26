using Telegram.Bot.Types;

namespace KeyBot.Handlers;

/// <summary>Управление состояниями интерактивных действий админа (grant, ban, unban).</summary>
public partial class AdminHandler
{
    // action: "grant" | "ban" | "unban", step: 1|2, target: userId
    private readonly ConcurrentDictionary<long, string> _adminAction = new();
    private readonly ConcurrentDictionary<long, int> _adminStep = new();
    private readonly ConcurrentDictionary<long, long> _adminTarget = new();

    private void SetAdminState(long adminId, string action, int step)
    {
        _adminAction[adminId] = action;
        _adminStep[adminId] = step;
    }

    private void ClearAdminState(long adminId)
    {
        _adminAction.TryRemove(adminId, out _);
        _adminStep.TryRemove(adminId, out _);
        _adminTarget.TryRemove(adminId, out _);
    }

    private static string ErrorText(UserLanguage lang) =>
        lang == UserLanguage.English
            ? "❌ Invalid ID. Try again:"
            : "❌ Неверный ID. Попробуйте ещё раз:";

    // ── Обработка входящего сообщения в интерактивном режиме ──────────────────

    public async Task<bool> TryHandleAdminStateAsync(Message message, CancellationToken ct)
    {
        if (message.From == null || message.Text == null)
            return false;
        if (!_adminStep.TryGetValue(message.From.Id, out var step))
            return false;

        var lang = await GetLang(message.From.Id);
        var action = _adminAction.GetValueOrDefault(message.From.Id, "");

        if (!long.TryParse(message.Text.Trim(), out var targetUserId) || targetUserId <= 0)
        {
            await _bot.SendMessage(message.Chat.Id, ErrorText(lang), cancellationToken: ct);
            return true;
        }

        ClearAdminState(message.From.Id);

        switch (action)
        {
            case "grant" when step == 1:
                SetAdminState(message.From.Id, "grant", 2);
                _adminTarget[message.From.Id] = targetUserId;
                await _bot.SendMessage(
                    message.Chat.Id,
                    Loc.GrantPromptDays(lang),
                    cancellationToken: ct
                );
                return true;

            case "grant" when step == 2:
                if (!int.TryParse(message.Text.Trim(), out var days) || days <= 0)
                {
                    await _bot.SendMessage(
                        message.Chat.Id,
                        "❌ "
                            + (
                                lang == UserLanguage.English
                                    ? "Invalid days. Try again:"
                                    : "Неверное количество дней. Попробуйте ещё раз:"
                            ),
                        cancellationToken: ct
                    );
                    return true;
                }
                await ExecuteGrantAsync(
                    message.From.Id,
                    message.Chat.Id,
                    _adminTarget.GetValueOrDefault(message.From.Id),
                    days,
                    lang,
                    ct
                );
                ClearAdminState(message.From.Id);
                return true;

            case "ban":
                await _userService.BanUserAsync(targetUserId);
                await _bot.SendMessage(
                    message.Chat.Id,
                    Loc.UserBanned(lang, targetUserId),
                    replyMarkup: _uiService.GetAdminMenu(lang),
                    cancellationToken: ct
                );
                return true;

            case "unban":
                await _userService.UnbanUserAsync(targetUserId);
                await _bot.SendMessage(
                    message.Chat.Id,
                    Loc.UserUnbanned(lang, targetUserId),
                    replyMarkup: _uiService.GetAdminMenu(lang),
                    cancellationToken: ct
                );
                return true;

            case "removesub":
                await HandleRemoveSubMessageAsync(message, targetUserId, ct);
                return true;
        }
        return false;
    }
}
