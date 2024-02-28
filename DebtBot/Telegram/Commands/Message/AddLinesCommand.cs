using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using DebtBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands.Message;

public class AddLinesCommand : ITelegramCommand
{
	private readonly ITelegramService _telegramService;
	private readonly IBillService _billService;

	public AddLinesCommand(
		ITelegramService telegramParserService,
		IBillService billService)
	{
		_telegramService = telegramParserService;
		_billService = billService;
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
                await botClient.SendTextMessageAsync(processedMessage.ChatId, $"Bill id not detected",
                     cancellationToken: cancellationToken,
                     parseMode: ParseMode.MarkdownV2);
                return;
            }
        }

        var lines = _telegramService.ParseLines(parsedText, processedMessage.UserSearchModels);
		_billService.AddLines(billId, lines, new UserSearchModel { TelegramId = processedMessage.FromId });
		await botClient.SendTextMessageAsync(
			processedMessage.ChatId, 
			$"Lines added to bill with id ```{billId}```", 
			cancellationToken: cancellationToken,
			parseMode: ParseMode.MarkdownV2);
	}
}