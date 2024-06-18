using DebtBot.Models.Enums;

namespace DebtBot.Models.User;

public class UserDisplayModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; }
    public long? TelegramId { get; set; }
    public string? TelegramUserName { get; set; }

    public override string ToString()
    {
        if (!String.IsNullOrEmpty(TelegramUserName))
            return $"{TelegramUserName}";

        if (TelegramId is not null)
            return $"<a href=\"tg://user?id={TelegramId}\">{DisplayName}</a>";

        return DisplayName;
    }
}