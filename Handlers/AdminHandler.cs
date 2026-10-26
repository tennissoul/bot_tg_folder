using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KeyBot.Handlers;

/// <summary>Основной класс AdminHandler — поля, DI, общие методы, панель, broadcast, shutdown.</summary>
public partial class AdminHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IUserService _userService;
    private readonly IKeyService _keyService;
    private readonly ITransactionService _transactionService;
    private readonly IUIService _uiService;
    private readonly long[] _adminIds;

    public AdminHandler(
        ITelegramBotClient bot,
        IUserService userService,
        IKeyService keyService,
        ITransactionService transactionService,
        IUIService uiService,
        long[] adminIds
    )
    {
        _bot = bot;
        _userService = userService;
        _keyService = keyService;
        _transactionService = transactionService;
        _uiService = uiService;
        _adminIds = adminIds;
    }

    // ── Общие утилиты ───────────────────────────────────────────────────────

    private bool IsAdmin(long userId) => _adminIds.Contains(userId);

    private async Task<UserLanguage> GetLang(long? userId) =>
        userId is > 0 ? await _userService.GetLanguageAsync(userId.Value) : UserLanguage.Russian;

    private async Task<bool> EnsureAdminAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (IsAdmin(cb.From.Id))
            return true;
        var lang = await GetLang(cb.From.Id);
        await _bot.AnswerCallbackQuery(
            cb.Id,
            Loc.NoPermission(lang),
            showAlert: true,
            cancellationToken: ct
        );
        return false;
    }

    // ── Админ панель ────────────────────────────────────────────────────────

    public async Task ShowAdminPanelAsync(long chatId, long userId, CancellationToken ct)
    {
        var lang = await GetLang(userId);
        if (!IsAdmin(userId))
        {
            await _bot.SendMessage(chatId, Loc.NoPermission(lang), cancellationToken: ct);
            return;
        }
        await _bot.SendMessage(
            chatId,
            Loc.AdminPanelTitle(lang),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: _uiService.GetAdminMenu(lang),
            cancellationToken: ct
        );
    }

    // ── Broadcast ────────────────────────────────────────────────────────────

    public async Task HandleBroadcastAsync(Message message, long[] adminIds, CancellationToken ct)
    {
        var lang = await GetLang(message.From?.Id);
        if (!IsAdmin(message.From?.Id ?? 0))
        {
            await _bot.SendMessage(message.Chat.Id, Loc.NoPermission(lang), cancellationToken: ct);
            return;
        }
        if (message.ReplyToMessage == null)
        {
            await _bot.SendMessage(
                message.Chat.Id,
                Loc.BroadcastReply(lang),
                cancellationToken: ct
            );
            return;
        }

        int success = 0,
            failed = 0;
        foreach (var uid in await _userService.GetAllUserIdsAsync())
        {
            try
            {
                await _bot.CopyMessage(
                    uid,
                    message.Chat.Id,
                    message.ReplyToMessage.MessageId,
                    cancellationToken: ct
                );
                success++;
                await Task.Delay(50, ct);
            }
            catch (Exception ex)
            {
                failed++;
                if (failed <= 5)
                    Console.WriteLine($"⚠️ Broadcast fail to {uid}: {ex.Message}");
            }
        }
        await _bot.SendMessage(
            message.Chat.Id,
            Loc.BroadcastComplete(lang, success, failed),
            cancellationToken: ct
        );
    }

    // ── Broadcast hint (кнопка) ──────────────────────────────────────────────

    public async Task HandleBroadcastCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(cb, ct))
            return;
        var lang = await GetLang(cb.From.Id);
        await _bot.SendMessage(
            cb.Message?.Chat.Id ?? cb.From.Id,
            Loc.BroadcastHint(lang),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: _uiService.GetAdminMenu(lang),
            cancellationToken: ct
        );
    }

    // ── Shutdown ─────────────────────────────────────────────────────────────

    public async Task HandleShutdownAsync(
        Message message,
        long[] adminIds,
        Action requestShutdown,
        CancellationToken ct
    )
    {
        var lang = await GetLang(message.From?.Id);
        if (!IsAdmin(message.From?.Id ?? 0))
        {
            await _bot.SendMessage(message.Chat.Id, Loc.NoPermission(lang), cancellationToken: ct);
            return;
        }
        await _bot.SendMessage(message.Chat.Id, Loc.ShutdownInitiated(lang), cancellationToken: ct);
        foreach (var adminId in adminIds)
        {
            if (adminId == message.From?.Id)
                continue;
            try
            {
                await _bot.SendMessage(
                    adminId,
                    Loc.BotStoppedBy(lang, message.From?.Id ?? 0),
                    cancellationToken: ct
                );
            }
            catch { }
        }
        requestShutdown();
    }

    // ── Shutdown (кнопка) ────────────────────────────────────────────────────

    public async Task HandleShutdownCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(cb, ct))
            return;
        var lang = await GetLang(cb.From.Id);
        await _bot.SendMessage(
            cb.Message?.Chat.Id ?? cb.From.Id,
            Loc.ShutdownConfirm(lang),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: ct
        );
    }
}
