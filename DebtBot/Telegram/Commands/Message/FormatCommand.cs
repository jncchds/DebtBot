using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands.Message;

public class FormatCommand : ITelegramCommand
{
    private readonly IPublishEndpoint _publishEndpoint;

    public FormatCommand(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public string CommandName => "/Format";

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var message = @"<code>/AddBill
Description (multiple rows, no empty lines)

Total with tips in bill currency
Currency Code (3 letters)
Payment currency code (3 letters, optional)

Payment_Amount1 User1
Payment_Amount2 User2...

Line_Description1 (one line)
Subtotal1
Ratio1_1 User1_1
Ratio2_1 User2_1...

Line_Description2
Subtotal2
Ratio1_2 User1_2
Ratio2_2 User2_2...

...</code>";

        await _publishEndpoint.Publish(new TelegramMessageRequested(processedMessage.ChatId, message));
    }
}