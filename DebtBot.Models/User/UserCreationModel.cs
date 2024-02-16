namespace DebtBot.Models.User;

public class UserCreationModel
{
    public string DisplayName { get; set; }
    public long? TelegramId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}