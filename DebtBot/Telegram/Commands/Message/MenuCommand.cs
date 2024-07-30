using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands.Message;

public class MenuCommand : ITelegramCommand
{
    public const string CommandString = "/Menu";
    private readonly IPublishEndpoint _publishEndpoint;

    public string CommandName => CommandString;

    public MenuCommand(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task ExecuteAsync(
        ProcessedMessage processedMessage, 
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var message = new TelegramMessageRequested(
            processedMessage.ChatId,
            $"<b>MENU</b>",
            InlineKeyboard:
            [
                new() {
                    new("Debts", DebtsCallbackQuery.CommandString),
                },
                new() {
                    new("Spendings", SpendingsCallbackQuery.CommandString)
                },
                new() {
                    new("Bills", BillsCommand.CommandString)
                }
            ]);

        await _publishEndpoint.Publish(message);

    }
}
