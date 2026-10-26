using Telegram.Bot.Types;

namespace KeyBot.Handlers;

/// <summary>Удаление подписки пользователя — кнопка, интерактив.</summary>
public partial class AdminHandler
{
    // ── Кнопка ───────────────────────────────────────────────────────────────

    public async Task HandleRemoveSubCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(cb, ct))
            return;
        SetAdminState(cb.From.Id, "removesub", 1);
        var lang = await GetLang(cb.From.Id);
        await _bot.SendMessage(
            cb.Message?.Chat.Id ?? cb.From.Id,
            Loc.RemoveSubPromptUser(lang),
            cancellationToken: ct
        );
    }

    // ── Исполнение ───────────────────────────────────────────────────────────

    public async Task HandleRemoveSubMessageAsync(Message message, long targetUserId, CancellationToken ct)
    {
        var lang = await GetLang(message.From?.Id);
        if (!IsAdmin(message.From?.Id ?? 0))
        {
            await _bot.SendMessage(message.Chat.Id, Loc.NoPermission(lang), cancellationToken: ct);
            return;
        }

        var count = await _keyService.RemoveUserSubscriptionsAsync(targetUserId);
        if (count > 0)
        {
            await _transactionService.RecordTransactionAsync(
                targetUserId,
                $"subscription_removed_by_admin_{message.From!.Id}",
                0
            );
            await _bot.SendMessage(
                message.Chat.Id,
                Loc.RemoveSubSuccess(lang, targetUserId),
                cancellationToken: ct
            );
        }
        else
        {
            await _bot.SendMessage(
                message.Chat.Id,
                Loc.RemoveSubNotFound(lang, targetUserId),
                cancellationToken: ct
            );
        }
    }
}
