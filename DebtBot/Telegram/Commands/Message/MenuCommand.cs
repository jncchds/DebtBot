using DebtBot.Interfaces.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Telegram.Commands.Message;

public class MenuCommand : ITelegramCommand
{
    public const string CommandString = "/Menu";
    public string CommandName => CommandString;


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
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Show bills", ShowBillsCommand.CommandString)
                ]
            }),
            parseMode: ParseMode.Html);
    }
}
