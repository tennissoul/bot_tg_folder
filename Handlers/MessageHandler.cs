using KeyBot;
using KeyBot.Models;
using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KeyBot.Handlers;

public class MessageHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IUserService _userService;
    private readonly IKeyService _keyService;
    private readonly IUIService _uiService;
    private readonly long[] _adminIds;
    private readonly ConcurrentDictionary<long, bool> _supportWaitState;
    private readonly ConcurrentDictionary<(long ChatId, int MessageId), long> _supportMessageMap =
        new();

    public MessageHandler(
        ITelegramBotClient bot,
        IUserService userService,
        IKeyService keyService,
        IUIService uiService,
        long[] adminIds,
        ConcurrentDictionary<long, bool> supportWaitState
    )
    {
        _bot = bot;
        _userService = userService;
        _keyService = keyService;
        _uiService = uiService;
        _adminIds = adminIds;
        _supportWaitState = supportWaitState;
    }

    public async Task HandleStartAsync(
        Message message,
        CancellationToken ct,
        string? refCode = null
    )
    {
        if (message.From == null)
            return;
        var user = message.From;

        if (await _userService.IsUserBannedAsync(user.Id))
        {
            var bl = await _userService.GetLanguageAsync(user.Id);
            await _bot.SendMessage(user.Id, Loc.Banned(bl), cancellationToken: ct);
            return;
        }

        bool isNew = await _userService.AddUserAsync(user.Id, user.Username ?? user.FirstName);
        var lang = await _userService.GetLanguageAsync(user.Id);

        await _bot.SendMessage(
            user.Id,
            Loc.Welcome(lang),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: _uiService.GetMainMenu(lang),
            cancellationToken: ct
        );

        if (isNew && !_adminIds.Contains(user.Id))
        {
            foreach (var adminId in _adminIds)
            {
                try
                {
                    await _bot.SendMessage(
                        adminId,
                        Loc.NewUserNotification(lang, user.Username, user.Id),
                        cancellationToken: ct
                    );
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"⚠️ New-user notification failed: {ex.GetType().Name}");
                }
            }
        }
    }

    public async Task HandleProfileAsync(Message message, CancellationToken ct)
    {
        if (message.From == null)
            return;
        var lang = await _userService.GetLanguageAsync(message.From.Id);

        var user = await _userService.GetUserAsync(message.From.Id);
        if (user == null)
        {
            await _bot.SendMessage(
                message.Chat.Id,
                Loc.ProfileNotFound(lang),
                cancellationToken: ct
            );
            return;
        }

        var activeKeys = await _keyService.GetUserKeysAsync(message.From.Id);
        string keysText = activeKeys.Any()
            ? "\n🔑 <b>"
                + Loc.ActiveKeys(lang)
                + "</b>\n"
                + string.Join(
                    "\n",
                    activeKeys.Select(k =>
                        $"<code>{k.Key}</code> ({Loc.Until(lang)} {k.ExpiresAt:yyyy-MM-dd})"
                    )
                )
            : "";

        string profileText = Loc.ProfileText(
            lang,
            message.From.Id,
            user.PurchaseCount,
            user.TotalSpent,
            user.FirstSeen.ToString("yyyy-MM-dd")
        );

        await _bot.SendMessage(
            message.Chat.Id,
            profileText + keysText,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: ct
        );
    }

    public async Task HandleSupportAsync(Message message, CancellationToken ct)
    {
        var userId = message.From?.Id ?? message.Chat.Id;
        var lang = await _userService.GetLanguageAsync(userId);
        await _bot.SendMessage(
            message.Chat.Id,
            Loc.SupportText(lang),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: _uiService.GetSupportMenu(lang),
            cancellationToken: ct
        );
    }

    public async Task HandleWriteSupportAsync(long userId, long chatId, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(userId);
        _supportWaitState[userId] = true;
        await _bot.SendMessage(chatId, Loc.WriteSupportPrompt(lang), cancellationToken: ct);
    }

    public async Task HandleSupportMessageAsync(
        Message message,
        long[] adminIds,
        CancellationToken ct
    )
    {
        if (message.From == null)
            return;
        if (!_supportWaitState.TryRemove(message.From.Id, out _))
            return;

        var lang = await _userService.GetLanguageAsync(message.From.Id);

        string forwardText =
            $"📩 <b>Сообщение от пользователя</b>\n"
            + $"От: @{message.From.Username ?? "нет"} (ID: <code>{message.From.Id}</code>)\n\n"
            + $"{message.Text}\n\n"
            + "<i>↩️ Ответьте реплаем на это сообщение чтобы написать пользователю</i>";

        foreach (var adminId in adminIds)
        {
            try
            {
                var sent = await _bot.SendMessage(
                    adminId,
                    forwardText,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    cancellationToken: ct
                );
                _supportMessageMap[(sent.Chat.Id, sent.MessageId)] = message.From.Id;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"⚠️ Support forwarding failed: {ex.GetType().Name}"
                );
            }
        }

        await _bot.SendMessage(
            message.Chat.Id,
            Loc.SupportSent(lang),
            replyMarkup: _uiService.GetMainMenu(lang),
            cancellationToken: ct
        );
    }

    public async Task<bool> TryHandleAdminReplyAsync(Message message, CancellationToken ct)
    {
        if (message.From == null || !_adminIds.Contains(message.From.Id))
            return false;
        if (message.ReplyToMessage == null)
            return false;
        if (
            !_supportMessageMap.TryGetValue(
                (message.ReplyToMessage.Chat.Id, message.ReplyToMessage.MessageId),
                out long targetUserId
            )
        )
            return false;

        try
        {
            await _bot.SendMessage(
                targetUserId,
                $"💬 <b>Ответ от поддержки:</b>\n\n{message.Text}",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                cancellationToken: ct
            );
            await _bot.SendMessage(
                message.Chat.Id,
                $"✅ Ответ отправлен пользователю <code>{targetUserId}</code>",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                cancellationToken: ct
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _bot.SendMessage(
                message.Chat.Id,
                "❌ Не удалось отправить ответ пользователю.",
                cancellationToken: ct
            );
            Console.Error.WriteLine($"⚠️ Support reply failed: {ex.GetType().Name}");
        }
        return true;
    }

    public async Task HandleClearAsync(Message message, CancellationToken ct)
    {
        if (message.From == null)
            return;
        int deleted = 0;
        for (int i = 0; i < 50; i++)
        {
            int msgId = message.MessageId - i;
            if (msgId <= 0)
                break;
            try
            {
                await _bot.DeleteMessage(message.Chat.Id, msgId, ct);
                deleted++;
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException)
            {
                // Older messages may already have been deleted or be outside Telegram's limits.
            }
        }
        var lang = await _userService.GetLanguageAsync(message.From.Id);
        var confirm = await _bot.SendMessage(
            message.Chat.Id,
            Loc.ClearedMessages(lang, deleted),
            cancellationToken: ct
        );
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        await _bot.DeleteMessage(message.Chat.Id, confirm.MessageId, ct);
    }
}
