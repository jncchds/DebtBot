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

public class AddLinesCommand : ITelegramCommand
{
	private readonly ITelegramService _telegramService;
	private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public AddLinesCommand(
		ITelegramService telegramParserService,
		IBillService billService,
        IPublishEndpoint publishEndpoint)
	{
		_telegramService = telegramParserService;
		_billService = billService;
        _publishEndpoint = publishEndpoint;
	}

	public const string CommandString = "/AddLines";
	public string CommandName => CommandString;

	public async Task ExecuteAsync(
		ProcessedMessage processedMessage, 
		ITelegramBotClient botClient,
		CancellationToken cancellationToken)
    {
        var trimmed = processedMessage.ProcessedText.Trim();

        var parsedText = processedMessage.ProcessedText;

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
                    "Bill id not detected"));
                return;
            }
        }

        var lines = _telegramService.ParseLines(parsedText, processedMessage.UserSearchModels);

        if (!lines.IsValid)
        {
            await _publishEndpoint.Publish(new SendTelegramMessage(
                processedMessage.ChatId,
                string.Join("\n", lines.Errors)));
            return;
        }

        _billService.AddLines(billId, lines.Result!, new UserSearchModel { TelegramId = processedMessage.FromId });
        await _publishEndpoint.Publish(new SendTelegramMessage(
            processedMessage.ChatId, 
            $"Lines added to bill with id ```{billId}```", 
            ParseMode: ParseMode.MarkdownV2));
	}
}