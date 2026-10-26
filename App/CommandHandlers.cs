using KeyBot.Configuration;
using KeyBot.Handlers;
using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KeyBot.App;

/// <summary>
/// Регистрирует словари команд и реализует встроенные хендлеры (/setup, /ai).
/// </summary>
internal class CommandHandlers
{
    private readonly ITelegramBotClient _bot;
    private readonly BotConfig _config;
    private readonly MessageHandler _messageHandler;
    private readonly PaymentHandler _paymentHandler;
    private readonly AdminHandler _adminHandler;
    private readonly IOpenRouterService _openRouterService;
    private readonly IUserService _userService;
    private readonly Action _requestShutdown;

    public CommandHandlers(
        ITelegramBotClient bot,
        BotConfig config,
        MessageHandler messageHandler,
        PaymentHandler paymentHandler,
        AdminHandler adminHandler,
        IOpenRouterService openRouterService,
        IUserService userService,
        Action requestShutdown
    )
    {
        _bot = bot;
        _config = config;
        _messageHandler = messageHandler;
        _paymentHandler = paymentHandler;
        _adminHandler = adminHandler;
        _openRouterService = openRouterService;
        _userService = userService;
        _requestShutdown = requestShutdown;
    }

    public Dictionary<string, Func<Message, CancellationToken, Task>> BuildMessageHandlers() =>
        new()
        {
            [Commands.Start] = (msg, ct) => _messageHandler.HandleStartAsync(msg, ct),
            [Commands.Buy] = _paymentHandler.HandleBuyButtonAsync,
            [Commands.Profile] = _messageHandler.HandleProfileAsync,
            [Commands.Support] = _messageHandler.HandleSupportAsync,
            [Commands.Setup] = HandleSetupAsync,
            [Commands.Clear] = _messageHandler.HandleClearAsync,
            [Commands.Stats] = (msg, ct) => _adminHandler.HandleStatsAsync(msg, ct),
            [Commands.Ban] = (msg, ct) => _adminHandler.HandleBanAsync(msg, ct),
            [Commands.Unban] = (msg, ct) => _adminHandler.HandleUnbanAsync(msg, ct),
            [Commands.Broadcast] = (msg, ct) =>
                _adminHandler.HandleBroadcastAsync(msg, _config.AdminIds, ct),
            [Commands.Shutdown] = (msg, ct) =>
                _adminHandler.HandleShutdownAsync(msg, _config.AdminIds, _requestShutdown, ct),
            [Commands.Kill] = (msg, ct) =>
                _adminHandler.HandleShutdownAsync(msg, _config.AdminIds, _requestShutdown, ct),
            [Commands.Admin] = HandleAdminAsync,
            [Commands.Grant] = _adminHandler.HandleGrantAsync,
            [Commands.Ai] = HandleAIAsync,
            [Commands.Ask] = HandleAIAsync,
        };

    // ── /admin ───────────────────────────────────────────────────────────────

    private async Task HandleAdminAsync(Message message, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(message.From?.Id ?? 0);
        await _adminHandler.ShowAdminPanelAsync(message.Chat.Id, message.From?.Id ?? 0, ct);
    }

    // ── /setup ───────────────────────────────────────────────────────────────

    private async Task HandleSetupAsync(Message message, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(message.From?.Id ?? 0);
        await _bot.SendMessage(
            chatId: message.Chat.Id,
            text: Loc.SetupInstruction(lang) + $"\n\n{_config.TermsUrl}/setup",
            parseMode: ParseMode.Html,
            cancellationToken: ct
        );
    }

    // ── /ai ──────────────────────────────────────────────────────────────────

    private async Task HandleAIAsync(Message message, CancellationToken ct)
    {
        if (message.From == null)
            return;

        var question = message.Text?.Replace("/ai", "").Replace("/ask", "").Trim();

        if (string.IsNullOrEmpty(question))
        {
            var lang = await _userService.GetLanguageAsync(message.From.Id);
            await _bot.SendMessage(
                message.Chat.Id,
                Loc.AiWelcome(lang),
                parseMode: ParseMode.Markdown,
                cancellationToken: ct
            );
            return;
        }

        await _bot.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: ct);

        var response = await _openRouterService.AskAsync(question, ct);

        if (!response.Contains("/buy") && !response.Contains("купить") && !response.Contains("🛒"))
            response +=
                "\n\n---\n💎 <b>Хотите попробовать?</b> Начните с пробного дня за 10₽ — нажмите 🛒 Купить ключ";

        await SendLongMessageAsync(message.Chat.Id, response, ct);
    }

    /// <summary>Отправляет длинный ответ, разбивая на части по 4000 символов.</summary>
    private async Task SendLongMessageAsync(long chatId, string text, CancellationToken ct)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            int take = Math.Min(4000, text.Length - pos);
            await _bot.SendMessage(
                chatId,
                text.Substring(pos, take),
                parseMode: ParseMode.Html,
                cancellationToken: ct
            );
            pos += take;
        }
    }
}
