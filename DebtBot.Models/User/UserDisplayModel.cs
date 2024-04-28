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
        var name = string.Equals(TelegramUserName, DisplayName, StringComparison.InvariantCultureIgnoreCase)
            ? DisplayName
            : $"{TelegramUserName} ({DisplayName})";

        if (!String.IsNullOrEmpty(TelegramUserName))
            return $"{name}";

        if (TelegramId is not null)
            return $"<a href=\"tg://user?id={TelegramId}\")>{DisplayName}</a>";

        return DisplayName;
    }
}