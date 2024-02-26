using DebtBot.Interfaces.Telegram;
using DebtBot.Telegram.CallbackQueries;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Telegram.Commands;

public class MenuCommand : ITelegramCommand
{
    public string CommandName => "/Menu";

    public async Task ExecuteAsync(
        ProcessedMessage processedMessage, 
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(
            processedMessage.ChatId, 
            $"<b>MENU</b>", 
            cancellationToken: cancellationToken,
            replyMarkup: new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Debts", DebtsCallbackQuery.CommandString),
                },
                [
                    InlineKeyboardButton.WithCallbackData("Spendings", SpendingsCallbackQuery.CommandString)
                ]
            }),
            parseMode: ParseMode.Html);
    }
}
