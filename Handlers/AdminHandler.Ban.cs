using Telegram.Bot.Types;

namespace KeyBot.Handlers;

/// <summary>Бан / разбан — команды и кнопки.</summary>
public partial class AdminHandler
{
    // ── /ban ─────────────────────────────────────────────────────────────────

    public async Task HandleBanAsync(Message message, CancellationToken ct)
    {
        var lang = await GetLang(message.From?.Id);
        if (!IsAdmin(message.From?.Id ?? 0))
        {
            await _bot.SendMessage(message.Chat.Id, Loc.NoPermission(lang), cancellationToken: ct);
            return;
        }
        var parts = (message.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (
            parts.Length < 2
            || !long.TryParse(parts[1], out var userId)
            || userId <= 0
        )
        {
            await _bot.SendMessage(message.Chat.Id, Loc.BanFormat(lang), cancellationToken: ct);
            return;
        }
        await _userService.BanUserAsync(userId);
        await _bot.SendMessage(
            message.Chat.Id,
            Loc.UserBanned(lang, userId),
            cancellationToken: ct
        );
    }

    // ── /unban ───────────────────────────────────────────────────────────────

    public async Task HandleUnbanAsync(Message message, CancellationToken ct)
    {
        var lang = await GetLang(message.From?.Id);
        if (!IsAdmin(message.From?.Id ?? 0))
        {
            await _bot.SendMessage(message.Chat.Id, Loc.NoPermission(lang), cancellationToken: ct);
            return;
        }
        var parts = (message.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (
            parts.Length < 2
            || !long.TryParse(parts[1], out var userId)
            || userId <= 0
        )
        {
            await _bot.SendMessage(
                message.Chat.Id,
                Loc.BanFormat(lang).Replace("/ban", "/unban"),
                cancellationToken: ct
            );
            return;
        }
        await _userService.UnbanUserAsync(userId);
        await _bot.SendMessage(
            message.Chat.Id,
            Loc.UserUnbanned(lang, userId),
            cancellationToken: ct
        );
    }

    // ── Кнопки ───────────────────────────────────────────────────────────────

    public async Task HandleBanCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(cb, ct))
            return;
        SetAdminState(cb.From.Id, "ban", 1);
        var lang = await GetLang(cb.From.Id);
        await _bot.SendMessage(
            cb.Message?.Chat.Id ?? cb.From.Id,
            Loc.BanPromptUser(lang),
            cancellationToken: ct
        );
    }

    public async Task HandleUnbanCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(cb, ct))
            return;
        SetAdminState(cb.From.Id, "unban", 1);
        var lang = await GetLang(cb.From.Id);
        await _bot.SendMessage(
            cb.Message?.Chat.Id ?? cb.From.Id,
            Loc.UnbanPromptUser(lang),
            cancellationToken: ct
        );
    }
}
