using KeyBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace KeyBot.Handlers;

public class AboutHandler(ITelegramBotClient _bot, IUserService _userService, IUIService _uiService)
{
    public async Task HandleAboutAsync(long chatId, long userId, CancellationToken ct) =>
        await _bot.SendMessage(
            chatId,
            Loc.AboutText(await _userService.GetLanguageAsync(userId)),
            parseMode: ParseMode.Html,
            replyMarkup: _uiService.GetBackButton(await _userService.GetLanguageAsync(userId)),
            cancellationToken: ct
        );
}
