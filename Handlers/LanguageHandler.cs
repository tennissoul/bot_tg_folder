using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace KeyBot.Handlers;

public class LanguageHandler(
    ITelegramBotClient _bot,
    IUserService _userService,
    IUIService _uiService
)
{
    public async Task HandleLanguageMenuAsync(long chatId, long userId, CancellationToken ct)
    {
        var lang = await _userService.GetLanguageAsync(userId);
        await _bot.SendMessage(
            chatId,
            Loc.ChooseLanguage(lang),
            replyMarkup: new InlineKeyboardMarkup([
                [InlineKeyboardButton.WithCallbackData("🇷🇺 Русский", "lang_ru")],
                [InlineKeyboardButton.WithCallbackData("🇬🇧 English", "lang_en")],
                [InlineKeyboardButton.WithCallbackData(Loc.BtnBack(lang), "back_to_main")],
            ]),
            cancellationToken: ct
        );
    }

    public async Task HandleSetLanguageAsync(
        long chatId,
        long userId,
        string langCode,
        CancellationToken ct
    )
    {
        var newLang = langCode == "en" ? UserLanguage.English : UserLanguage.Russian;
        await _userService.SetLanguageAsync(userId, newLang);

        await _bot.SendMessage(
            chatId,
            Loc.LanguageChanged(newLang),
            replyMarkup: _uiService.GetMainMenu(newLang),
            cancellationToken: ct
        );
    }
}
