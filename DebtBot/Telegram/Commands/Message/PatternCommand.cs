using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands.Message;

public class PatternCommand : ITelegramCommand
{
    public const string CommandString = "/Pattern";
    private readonly IPublishEndpoint _publishEndpoint;

    public string CommandName => CommandString;

    public PatternCommand(IPublishEndpoint publishEndpoint)
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
            @"<pre><code class=""raw"">
Bill Description(multiple rows, no empty lines)


Bill total with tips in bill currency
Currency Code [3 letters]
Payment currency code [3 letters, optional]

PaymentAmount1 User1
PaymentAmount2 User2
...
PaymentAmountN UserN

Line 1 Description [one line]
Line 1 Subtotal
Ratio1_1 User1_1
Ratio2_1 User2_1
...
RatioK_1 UserK_1

Line 2 Description [one line]
Line 2 Subtotal
Ratio1_2 User1_2
Ratio2_2 User2_2
...
RatioL_2 UserL_2

...

Line M Description [one line]
Line M Subtotal
Ratio1_M User1_M
Ratio2_M User2_M
...
RatioP_M UserP_M</code></pre>");

        await _publishEndpoint.Publish(message);

    }
}
