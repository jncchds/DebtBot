namespace DebtBot.Models;

public class UserModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; }
    public long? TelegramId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}