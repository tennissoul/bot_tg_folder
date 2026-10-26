using System.Text.RegularExpressions;
using KeyBot;
using KeyBot.Models;
using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KeyBot.Handlers;

public class PaymentHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IPaymentService _paymentService;
    private readonly IKeyService _keyService;
    private readonly ITransactionService _transactionService;
    private readonly IUIService _uiService;
    private readonly IUserService _userService;
    private readonly long[] _adminIds;

    public PaymentHandler(
        ITelegramBotClient bot,
        IPaymentService paymentService,
        IKeyService keyService,
        ITransactionService transactionService,
        IUIService uiService,
        IUserService userService,
        long[] adminIds
    )
    {
        _bot = bot;
        _paymentService = paymentService;
        _keyService = keyService;
        _transactionService = transactionService;
        _uiService = uiService;
        _userService = userService;
        _adminIds = adminIds;
    }

    public async Task HandleBuyButtonAsync(Message message, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(message.From?.Id ?? 0);
        await _bot.SendMessage(
            message.Chat.Id,
            Loc.BuyTitle(lang),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: _uiService.GetTariffsMenu(lang),
            cancellationToken: ct
        );
    }

    public async Task HandleTariffSelectionAsync(
        CallbackQuery callback,
        long userId,
        int days,
        decimal price,
        CancellationToken ct
    )
    {
        if (callback.Message == null)
            return;

        // Защита от невалидных параметров
        if (days <= 0 || days > 3650 || price < 0)
        {
            await _bot.AnswerCallbackQuery(
                callback.Id,
                "❌ Invalid tariff parameters.",
                showAlert: true,
                cancellationToken: ct
            );
            return;
        }

        var lang = await _userService.GetLanguageAsync(userId);

        var tariff = Tariffs.GetByDays(days);
        if (tariff == null)
        {
            await _bot.AnswerCallbackQuery(
                callback.Id,
                Loc.InvalidTariff(lang),
                showAlert: true,
                cancellationToken: ct
            );
            return;
        }

        // Получаем ссылку на оплату через CryptoPay
        string paymentLink = await _paymentService.CreatePaymentLinkAsync(tariff, userId, ct);

        if (string.IsNullOrEmpty(paymentLink))
        {
            await _bot.AnswerCallbackQuery(
                callback.Id,
                Loc.PaymentError(lang),
                showAlert: true,
                cancellationToken: ct
            );
            return;
        }

        // ПРОВЕРКА НА ТЕСТОВЫЙ РЕЖИМ
        if (paymentLink.StartsWith("TEST_MODE_"))
        {
            // Тестовый режим - сразу выдаём ключ без оплаты
            await _bot.AnswerCallbackQuery(
                callback.Id,
                Loc.TestModePrefix(lang) + ": key created without payment",
                showAlert: true,
                cancellationToken: ct
            );

            // Создаём ключ
            string key = await _keyService.GenerateKeyAsync(userId, days);
            await _transactionService.RecordTransactionAsync(
                userId,
                $"test_{days}d_{price}rub",
                price
            );

            var expiresAt = DateTime.UtcNow.AddDays(days);

            string successText = Loc.TestModeSuccess(
                lang,
                days,
                price,
                key,
                expiresAt.ToString("yyyy-MM-dd HH:mm")
            );

            await _bot.SendMessage(
                callback.Message.Chat.Id,
                successText,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: _uiService.GetMainMenu(lang),
                cancellationToken: ct
            );

            // Уведомляем администраторов о тестовой покупке
            string adminText = Loc.AdminTestSale(lang, userId, days, price);

            foreach (var adminId in _adminIds)
            {
                try
                {
                    await _bot.SendMessage(
                        adminId,
                        adminText,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: ct
                    );
                }
                catch { }
            }

            return;
        }

        // РЕАЛЬНЫЙ ПЛАТЁЖ
        string paymentMsg = Loc.TariffPaymentInfo(lang, tariff.Name, tariff.PriceRub, days);

        var keyboard = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithUrl(
                Loc.PayButton(lang),
                paymentLink
            )
        );

        await _bot.SendMessage(
            callback.Message.Chat.Id,
            paymentMsg,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct
        );

        await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
    }

    public async Task HandleSuccessfulPaymentAsync(
        long userId,
        int days,
        decimal amount,
        CancellationToken ct,
        Action<long, string> notifyAdmins
    )
    {
        var lang = await _userService.GetLanguageAsync(userId);

        // Создаём ключ
        string key = await _keyService.GenerateKeyAsync(userId, days);

        // Записываем транзакцию
        await _transactionService.RecordTransactionAsync(
            userId,
            $"key_{days}d_{amount}rub",
            amount
        );

        var expiresAt = DateTime.UtcNow.AddDays(days);

        string successText = Loc.PaymentSuccess(
            lang,
            days,
            amount,
            key,
            expiresAt.ToString("yyyy-MM-dd HH:mm")
        );

        await _bot.SendMessage(
            userId,
            successText,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: _uiService.GetMainMenu(lang),
            cancellationToken: ct
        );

        // Уведомляем администраторов
        string adminText = Loc.AdminNewSale(lang, userId, days, amount);
        notifyAdmins(userId, adminText);
    }
}
