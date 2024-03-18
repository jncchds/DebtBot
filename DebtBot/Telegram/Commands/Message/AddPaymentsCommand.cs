using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using DebtBot.Services;
using MassTransit;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands.Message;

public class AddPaymentsCommand : ITelegramCommand
{
    private readonly ITelegramService _telegramService;
    private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public AddPaymentsCommand(
        ITelegramService telegramParserService,
        IBillService billService,
        IPublishEndpoint publishEndpoint)
    {
        _telegramService = telegramParserService;
        _billService = billService;
        _publishEndpoint = publishEndpoint;
    }

    public const string CommandString = "/AddPayments";
    public string CommandName => CommandString;

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var trimmed = processedMessage.ProcessedText.Trim();

        string parsedText = processedMessage.ProcessedText;

        Guid billId;

        if (processedMessage.ObjectType == ObjectType.Bill && processedMessage.ObjectId != null)
        {
            billId = processedMessage.ObjectId.Value;
        }
        else
        {
            var index = trimmed.IndexOf("\n", StringComparison.InvariantCulture);
            var guidString = trimmed.Substring(0, index);
            parsedText = trimmed.Substring(index + 1);

            if (!Guid.TryParse(guidString, out billId))
            {
                await _publishEndpoint.Publish(new SendTelegramMessage(
                    processedMessage.ChatId, 
                    $"Bill id not detected"));
                return;
            }
        }

        var payments = _telegramService.ParsePayments(parsedText, processedMessage.UserSearchModels);
        _billService.AddPayments(billId, payments, new UserSearchModel { TelegramId = processedMessage.FromId });
        await _publishEndpoint.Publish(new SendTelegramMessage(
            processedMessage.ChatId,
            $"Payments added to bill with id ```{billId}```", ParseMode: ParseMode.MarkdownV2));
    }
}