namespace DebtBot.Messages;

public class EnsureContact
{
    public Guid UserId { get; set; }
    public Guid ContactUserId { get; set; }
    public string DisplayName { get; set; }
}
