using DebtBot.Models.Enums;

namespace DebtBot.Models.User;

public class UserModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; }
    public long? TelegramId { get; set; }
    public string? TelegramUserName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool TelegramBotEnabled { get; set; }
    public UserRole Role { get; set; }
}