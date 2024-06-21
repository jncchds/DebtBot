using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface ISubscriptionService
{
    void RequestSubscription(Guid userId, Guid contactId, bool forceSubscribe = false);
    void ConfirmSubscription(Guid userId, Guid contactId);
    void RemoveSubscription(Guid userId, Guid contactId);
}