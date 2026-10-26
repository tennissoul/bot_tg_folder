using Telegram.Bot.Types;

namespace KeyBot.Handlers;

/// <summary>Статистика — команда /stats и кнопка в админ-панели.</summary>
public partial class AdminHandler
{
    // ── /stats ───────────────────────────────────────────────────────────────

    public async Task HandleStatsAsync(Message message, CancellationToken ct)
    {
        var lang = await GetLang(message.From?.Id);
        if (!IsAdmin(message.From?.Id ?? 0))
        {
            await _bot.SendMessage(message.Chat.Id, Loc.NoPermission(lang), cancellationToken: ct);
            return;
        }
        var (users, revenue) = await GetStatsAsync();
        await _bot.SendMessage(
            message.Chat.Id,
            Loc.StatsTitle(lang, users, revenue, 0),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: ct
        );
    }

    // ── Кнопка статистики ───────────────────────────────────────────────────

    public async Task HandleStatsCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(cb, ct))
            return;
        var lang = await GetLang(cb.From.Id);
        var (users, revenue) = await GetStatsAsync();
        await _bot.SendMessage(
            cb.Message?.Chat.Id ?? cb.From.Id,
            Loc.StatsTitle(lang, users, revenue, 0),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: _uiService.GetAdminMenu(lang),
            cancellationToken: ct
        );
    }

    private async Task<(int users, decimal revenue)> GetStatsAsync()
    {
        var userIds = await _userService.GetAllUserIdsAsync();
        var revenue = await _transactionService.GetTotalRevenueAsync();
        return (userIds.Count, revenue);
    }
}
