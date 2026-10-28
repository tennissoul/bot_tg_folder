using Telegram.Bot.Types;

namespace KeyBot.Handlers;

/// <summary>Выдача подписки — /grant, кнопка, интерактив.</summary>
public partial class AdminHandler
{
    // ── /grant ───────────────────────────────────────────────────────────────

    public async Task HandleGrantAsync(Message message, CancellationToken ct)
    {
        var lang = await GetLang(message.From?.Id);
        if (!IsAdmin(message.From?.Id ?? 0))
        {
            await _bot.SendMessage(message.Chat.Id, Loc.NoPermission(lang), cancellationToken: ct);
            return;
        }
        var parts = (message.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (
            parts.Length != 3
            || !long.TryParse(parts[1], out var targetUserId)
            || targetUserId <= 0
            || !int.TryParse(parts[2], out var days)
            || days is <= 0 or > 3650
        )
        {
            await _bot.SendMessage(
                message.Chat.Id,
                Loc.GrantInvalidFormat(lang),
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                cancellationToken: ct
            );
            return;
        }
        await ExecuteGrantAsync(message.From!.Id, message.Chat.Id, targetUserId, days, lang, ct);
    }

    // ── Кнопка ───────────────────────────────────────────────────────────────

    public async Task HandleGrantCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(cb, ct))
            return;
        SetAdminState(cb.From.Id, "grant", 1);
        var lang = await GetLang(cb.From.Id);
        await _bot.SendMessage(
            cb.Message?.Chat.Id ?? cb.From.Id,
            Loc.GrantPromptUser(lang),
            cancellationToken: ct
        );
    }

    // ── Исполнение ───────────────────────────────────────────────────────────

    private async Task ExecuteGrantAsync(
        long adminId,
        long chatId,
        long targetUserId,
        int days,
        UserLanguage lang,
        CancellationToken ct
    )
    {
        if (targetUserId <= 0 || days is <= 0 or > 3650)
        {
            await _bot.SendMessage(
                chatId,
                Loc.GrantInvalidFormat(lang),
                cancellationToken: ct
            );
            return;
        }

        if (await _userService.GetUserAsync(targetUserId) == null)
            await _userService.AddUserAsync(targetUserId, $"user_{targetUserId}");

        var key = await _keyService.GenerateKeyAsync(targetUserId, days);
        await _transactionService.RecordTransactionAsync(
            targetUserId,
            $"grant_{days}d_by_{adminId}",
            0
        );

        await _bot.SendMessage(
            chatId,
            Loc.GrantSuccess(lang, targetUserId, days),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: ct
        );

        try
        {
            var ul = await _userService.GetLanguageAsync(targetUserId);
            var exp = DateTime.UtcNow.AddDays(days).ToString("yyyy-MM-dd HH:mm");
            await _bot.SendMessage(
                targetUserId,
                Loc.PaymentSuccess(ul, days, 0, key, exp),
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: _uiService.GetMainMenu(ul),
                cancellationToken: ct
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"⚠️ Grant notification failed: {ex.GetType().Name}");
        }
    }
}
