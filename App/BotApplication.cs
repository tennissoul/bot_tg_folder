using System.Net.Http;
using KeyBot.Configuration;
using KeyBot.Handlers;
using KeyBot.Services;
using KeyBot.Utils;
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

        using var telegramHttpClient = await HttpClientFactory.BuildHttpClientAsync(cts.Token);
        using var paymentHttpClient = await HttpClientFactory.BuildHttpClientAsync(cts.Token);
        using var assistantHttpClient = await HttpClientFactory.BuildHttpClientAsync(cts.Token);
        var bot = new TelegramBotClient(_config.BotToken, telegramHttpClient);

        try
        {
            var me = await bot.GetMe(cts.Token);
            Console.WriteLine($"✓ Подключено как @{me.Username}");

            // Сбрасываем старые pending updates (чтобы не прилетел /shutdown из прошлой сессии)
            await bot.DropPendingUpdates(cts.Token);
            Console.WriteLine("✓ Pending updates сброшены");
        }
        catch (ApiRequestException ex)
        {
            Console.Error.WriteLine(
                $"❌ Не удалось подключиться к Telegram ({ex.GetType().Name})"
            );
            throw;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine(
                $"❌ Не удалось подключиться к Telegram ({ex.GetType().Name})"
            );
            throw;
        }

        var (router, paymentHandler, paymentService, paymentProcessor) = BuildRouter(
            bot,
            paymentHttpClient,
            assistantHttpClient
        );

        bot.OnError += (ex, source) =>
        {
            if (ex is OperationCanceledException)
                return Task.CompletedTask;
            Console.Error.WriteLine($"⚠️ [PollingError] {source}: {ex.GetType().Name}");
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
                Console.Error.WriteLine($"❌ [OnMessage] {ex.GetType().Name}");
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
                Console.Error.WriteLine($"❌ [OnUpdate] {ex.GetType().Name}");
            }
        };

        Console.WriteLine("🤖 Бот успешно запущен и работает");
        _dbManager.StartAutoSave();
        CryptoPayWebhookHost? webhookHost = null;

        try
        {
            if (!string.IsNullOrEmpty(_config.CryptoPayToken))
            {
                webhookHost = CryptoPayWebhookHost.Create(
                    _config.CryptoPayWebhookListenUrl,
                    paymentService,
                    paymentProcessor
                );
                await webhookHost.StartAsync(cts.Token);
            }

            _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("⌨️ Получен сигнал остановки...");
                RequestShutdown();
            };
            if (webhookHost is null)
                await _tcs.Task;
            else
            {
                var completedTask = await Task.WhenAny(_tcs.Task, webhookHost.Completion);
                if (completedTask != _tcs.Task)
                    throw new InvalidOperationException("CryptoPay webhook listener stopped unexpectedly.");
                await _tcs.Task;
            }
        }
        finally
        {
            Console.WriteLine("🛑 Остановка бота...");
            try
            {
                if (webhookHost is not null)
                    await webhookHost.StopAsync(CancellationToken.None);
            }
            finally
            {
                try
                {
                    if (webhookHost is not null)
                        await webhookHost.DisposeAsync();
                }
                finally
                {
                    _cts = null;
                    await _dbManager.StopAsync();
                    Console.WriteLine("✓ Бот остановлен");
                }
            }
        }
    }

    private void RequestShutdown()
    {
        Console.WriteLine("🛑 Получен запрос на остановку бота.");
        _cts?.Cancel();
        _tcs?.TrySetResult();
    }

    // ── Сборка роутера ───────────────────────────────────────────────────────

    private (
        UpdateRouter Router,
        PaymentHandler PaymentHandler,
        IPaymentService PaymentService,
        CryptoPayPaymentProcessor PaymentProcessor
    )
        BuildRouter(
        ITelegramBotClient bot,
        HttpClient paymentHttpClient,
        HttpClient assistantHttpClient
    )
    {
        var userService = new UserService(_database) as IUserService;
        var keyService = new KeyService(_database) as IKeyService;
        var transactionService = new TransactionService(_database) as ITransactionService;
        var paymentService = new CryptoPayService(
            _config.CryptoPayToken,
            paymentHttpClient,
            _config.AllowTestPayments,
            _config.CryptoPayWebhookUrl
        );
        var uiService = new UIService() as IUIService;
        var copilotService = new OpenRouterService(assistantHttpClient, _config.OpenRouterKey)
            as IOpenRouterService;

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
            _dbManager,
            uiService,
            userService,
            _config.AdminIds
        );
        var paymentProcessor = new CryptoPayPaymentProcessor(
            bot,
            keyService,
            transactionService,
            _dbManager,
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

        var router = new UpdateRouter(
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
            adminHandler,
            new UpdateRateLimiter()
        );
        return (router, paymentHandler, paymentService, paymentProcessor);
    }
}
