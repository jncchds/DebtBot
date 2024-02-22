using DebtBot.Models.Enums;
using DebtBot.Models.User;

namespace DebtBot.Telegram;

public class ProcessedMessage
{
    public string ProcessedText { get; set; }
	public List<UserSearchModel> UserSearchModels { get; set; }
	public string? BotCommand { get; set; }
	public long ChatId { get; set; }
	public long FromId { get; set; }
    internal ObjectType? ObjectType;
    internal Guid? ObjectId;
}
