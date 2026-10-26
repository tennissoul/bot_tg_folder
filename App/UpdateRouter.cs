using System.Net.Http;
using KeyBot.Handlers;
using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace KeyBot.App;

/// <summary>Принимает все апдейты от Telegram и направляет их нужному хендлеру.</summary>
internal class UpdateRouter
{
    private readonly ITelegramBotClient _bot;
    private readonly MessageHandler _messageHandler;
    private readonly IUIService _uiService;
    private readonly IUserService _userService;
    private readonly long[] _adminIds;
    private readonly ConcurrentDictionary<long, bool> _supportWaitState;
    private readonly AdminHandler _adminHandler;
    private readonly ReferralHandler _referralHandler;

    private readonly Dictionary<string, Func<Message, CancellationToken, Task>> _messageHandlers;
    private readonly Dictionary<
        string,
        Func<CallbackQuery, CancellationToken, Task>
    > _callbackHandlers;
    private readonly Func<CallbackQuery, CancellationToken, Task> _tariffHandler;

    public UpdateRouter(
        ITelegramBotClient bot,
        MessageHandler messageHandler,
        IUIService uiService,
        IUserService userService,
        long[] adminIds,
        ConcurrentDictionary<long, bool> supportWaitState,
        Dictionary<string, Func<Message, CancellationToken, Task>> messageHandlers,
        Dictionary<string, Func<CallbackQuery, CancellationToken, Task>> callbackHandlers,
        Func<CallbackQuery, CancellationToken, Task> tariffHandler,
        ReferralHandler referralHandler,
        AdminHandler adminHandler
    )
    {
        _bot = bot;
        _messageHandler = messageHandler;
        _uiService = uiService;
        _userService = userService;
        _adminIds = adminIds;
        _supportWaitState = supportWaitState;
        _messageHandlers = messageHandlers;
        _callbackHandlers = callbackHandlers;
        _tariffHandler = tariffHandler;
        _referralHandler = referralHandler;
        _adminHandler = adminHandler;
    }

    // ── Точка входа (v22 OnMessage / OnUpdate) ───────────────────────────────

    public Task HandleMessageDirectAsync(Message message, CancellationToken ct) =>
        HandleMessageAsync(message, ct);

    public Task HandleUpdateDirectAsync(Update update, CancellationToken ct)
    {
        if (update.CallbackQuery is not null)
            return HandleCallbackAsync(update.CallbackQuery, ct);
        return Task.CompletedTask;
    }

    public Task HandleErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException)
            return Task.CompletedTask;

        if (exception is ApiRequestException { Parameters.RetryAfter: not null } apiEx)
        {
            Console.WriteLine($"⏳ Flood wait {apiEx.Parameters.RetryAfter}s");
            return Task.CompletedTask;
        }

        if (exception is HttpRequestException httpEx)
        {
            Console.WriteLine($"🌐 Нет связи с Telegram: {httpEx.Message}");
            return Task.CompletedTask;
        }

        Console.WriteLine($"⚠️ Ошибка API [{exception.GetType().Name}]: {exception.Message}");
        return Task.CompletedTask;
    }

    // ── Сообщения ────────────────────────────────────────────────────────────

    private async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        if (message.From == null)
            return;

        var userId = message.From.Id;
        var text = message.Text;

        // 1. Ответ админа в поддержку
        if (await _messageHandler.TryHandleAdminReplyAsync(message, ct))
            return;

        // 2. Сообщение без текста (стикеры, фото) — только в режиме поддержки
        if (text == null)
        {
            await HandleNonTextMessageAsync(userId, message, ct);
            return;
        }

        // 3. /start с реферальным кодом
        if (text.StartsWith(Commands.Start))
        {
            await HandleStartCommandAsync(message, userId, text, ct);
            return;
        }

        // 4. Интерактивное действие админа
        if (await TryHandleAdminStateSafeAsync(message, ct))
            return;

        // 5. Зарегистрированная команда
        if (await TryHandleRegisteredCommandAsync(message, text, userId, ct))
            return;

        // 6. Fallback — поддержка или меню
        await HandleFallbackAsync(userId, message, ct);
    }

    // ── Раздельные обработчики ───────────────────────────────────────────────

    private async Task HandleNonTextMessageAsync(long userId, Message message, CancellationToken ct)
    {
        if (_supportWaitState.ContainsKey(userId))
            try
            {
                await _messageHandler.HandleSupportMessageAsync(message, _adminIds, ct);
            }
            catch (Exception ex)
            {
                LogError($"Support msg from {userId}", ex);
            }
    }

    private async Task HandleStartCommandAsync(
        Message message,
        long userId,
        string text,
        CancellationToken ct
    )
    {
        var parts = text.Split(' ', 2);
        string? refCode = parts.Length > 1 ? parts[1].Trim() : null;

        if (!string.IsNullOrEmpty(refCode))
            try
            {
                await _referralHandler.TryRegisterReferralAsync(userId, refCode, ct);
            }
            catch (Exception ex)
            {
                LogError($"Referral register for {userId}", ex);
            }

        await _messageHandler.HandleStartAsync(message, ct, refCode);
    }

    private async Task<bool> TryHandleAdminStateSafeAsync(Message message, CancellationToken ct)
    {
        try
        {
            return await _adminHandler.TryHandleAdminStateAsync(message, ct);
        }
        catch (Exception ex)
        {
            LogError($"Admin state for {message.From?.Id}", ex);
            return false;
        }
    }

    private async Task<bool> TryHandleRegisteredCommandAsync(
        Message message,
        string text,
        long userId,
        CancellationToken ct
    )
    {
        foreach (var (cmd, handler) in _messageHandlers)
        {
            if (!text.StartsWith(cmd))
                continue;
            try
            {
                await handler(message, ct);
            }
            catch (Exception ex)
            {
                LogError($"Cmd '{cmd}' from {userId}", ex);
            }
            return true;
        }
        return false;
    }

    private async Task HandleFallbackAsync(long userId, Message message, CancellationToken ct)
    {
        try
        {
            if (_supportWaitState.ContainsKey(userId))
            {
                await _messageHandler.HandleSupportMessageAsync(message, _adminIds, ct);
                return;
            }
            var lang = await _userService.GetLanguageAsync(userId);
            await _bot.SendMessage(
                message.Chat.Id,
                Loc.UseMenuButtons(lang),
                replyMarkup: _uiService.GetMainMenu(lang),
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            LogError($"Fallback handler for {userId}", ex);
        }
    }

    // ── Callbacks ────────────────────────────────────────────────────────────

    private async Task HandleCallbackAsync(CallbackQuery callback, CancellationToken ct)
    {
        if (callback.From == null || callback.Data == null)
            return;

        try
        {
            await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
        }
        catch
        { /* ignore callback ack failures */
        }

        try
        {
            if (_callbackHandlers.TryGetValue(callback.Data, out var handler))
            {
                await handler(callback, ct);
                return;
            }

            if (callback.Data.StartsWith(CallbackData.TariffPrefix))
                await _tariffHandler(callback, ct);
        }
        catch (Exception ex)
        {
            LogError($"Callback '{callback.Data}' from {callback.From.Id}", ex);
        }
    }

    private static void LogError(string context, Exception ex)
    {
        Console.WriteLine($"❌ [{context}] {ex.GetType().Name}: {ex.Message}");
    }
}
