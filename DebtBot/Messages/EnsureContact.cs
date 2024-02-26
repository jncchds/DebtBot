namespace DebtBot.Messages;

public record EnsureContact(
    Guid UserId, 
    Guid ContactUserId, 
    string DisplayName);
