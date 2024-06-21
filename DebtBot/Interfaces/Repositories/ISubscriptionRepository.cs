namespace DebtBot.Interfaces.Repositories;
public interface ISubscriptionRepository
{
    void Add(Guid userId, Guid contactId, bool subscriptionStatus = false);
    void SetSubscriptionStatusConfirmed(Guid userId, Guid contactId);
    void RemoveSubscription(Guid userId, Guid contactId);
}