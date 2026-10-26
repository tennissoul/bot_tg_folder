using System.Net.Http;
using KeyBot.Configuration;
using KeyBot.Handlers;
using KeyBot.Services;
using Telegram.Bot;

namespace KeyBot.App;

/// <summary>
/// Точка входа приложения бота: инициализация, запуск, остановка.
/// </summary>
public class BotApplication
{
    private readonly BotConfig _config;
    private readonly Database _database;
    private readonly DatabaseManager _dbManager;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _tcs;

    public BotApplication(BotConfig config)
    {
        _config = config;
        _database = new Database();
        _dbManager = new(_database, _config.DataPath);
    }

    public async Task RunAsync()
    {
        _dbManager.Load();
        using var cts = new CancellationTokenSource();
        _cts = cts;

        var bot = new TelegramBotClient(_config.BotToken, HttpClientFactory.BuildHttpClient());

        try
        {
            var me = await bot.GetMe(cts.Token);
            Console.WriteLine($"✓ Подключено как @{me.Username}");

            // Сбрасываем старые pending updates (чтобы не прилетел /shutdown из прошлой сессии)
            await bot.DropPendingUpdates(cts.Token);
            Console.WriteLine("✓ Pending updates сброшены");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Не удалось подключиться к Telegram: {ex.Message}");
            return;
        }

        var router = BuildRouter(bot);

        bot.OnError += (ex, source) =>
        {
            if (ex is OperationCanceledException)
                return Task.CompletedTask;
            Console.WriteLine($"⚠️ [PollingError] {source}: {ex.Message}");
            return Task.CompletedTask;
        };

        bot.OnMessage += async (msg, _) =>
        {
            try
            {
                await router.HandleMessageDirectAsync(msg, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [OnMessage] {ex.Message}");
            }
        };

        bot.OnUpdate += async (update) =>
        {
            try
            {
                await router.HandleUpdateDirectAsync(update, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [OnUpdate] {ex.Message}");
            }
        };

        Console.WriteLine("🤖 Бот успешно запущен и работает");
        _dbManager.StartAutoSave();

        try
        {
            _tcs = new TaskCompletionSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("⌨️ Получен сигнал остановки...");
                RequestShutdown();
            };
            await _tcs.Task;
        }
        finally
        {
            Console.WriteLine("🛑 Остановка бота...");
            _dbManager.Stop();
            _cts = null;
            Console.WriteLine("✓ Бот остановлен");
        }
    }

    private void RequestShutdown()
    {
        Console.WriteLine($"🔍 Shutdown triggered! Стэк: {Environment.StackTrace}");
        _cts?.Cancel();
        _tcs?.TrySetResult();
    }

    // ── Сборка роутера ───────────────────────────────────────────────────────

    private UpdateRouter BuildRouter(ITelegramBotClient bot)
    {
        var userService = new UserService(_database) as IUserService;
        var keyService = new KeyService(_database) as IKeyService;
        var transactionService = new TransactionService(_database) as ITransactionService;
        var paymentService = new CryptoPayService(_config.CryptoPayToken) as IPaymentService;
        var uiService = new UIService() as IUIService;
        var copilotService =
            new OpenRouterService(new(), _config.OpenRouterKey) as IOpenRouterService;

        var supportWaitState = new ConcurrentDictionary<long, bool>();

        var messageHandler = new MessageHandler(
            bot,
            userService,
            keyService,
            uiService,
            _config.AdminIds,
            supportWaitState
        );
        var paymentHandler = new PaymentHandler(
            bot,
            paymentService,
            keyService,
            transactionService,
            uiService,
            userService,
            _config.AdminIds
        );
        var adminHandler = new AdminHandler(
            bot,
            userService,
            keyService,
            transactionService,
            uiService,
            _config.AdminIds
        );

        var commandHandlers = new CommandHandlers(
            bot,
            _config,
            messageHandler,
            paymentHandler,
            adminHandler,
            copilotService,
            userService,
            RequestShutdown
        );

        var referralService = (IReferralService)new ReferralService(_database, null);
        var aboutHandler = new AboutHandler(bot, userService, uiService);
        var subscriptionHandler = new SubscriptionHandler(bot, userService, keyService, uiService);
        var languageHandler = new LanguageHandler(bot, userService, uiService);
        var referralHandler = new ReferralHandler(
            bot,
            userService,
            referralService,
            uiService,
            _config.BotUsername,
            () => userService.GetAllUserIdsAsync()
        );

        var callbackHandlers = new CallbackHandlers(
            bot,
            messageHandler,
            paymentHandler,
            aboutHandler,
            subscriptionHandler,
            referralHandler,
            languageHandler,
            adminHandler,
            userService,
            uiService
        );

        return new UpdateRouter(
            bot,
            messageHandler,
            uiService,
            userService,
            _config.AdminIds,
            supportWaitState,
            commandHandlers.BuildMessageHandlers(),
            callbackHandlers.BuildCallbackHandlers(),
            callbackHandlers.HandleTariffPurchaseAsync,
            referralHandler,
            adminHandler
        );
    }
}
