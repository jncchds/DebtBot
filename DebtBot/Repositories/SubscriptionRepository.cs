using DebtBot.DB;
using DebtBot.DB.Entities;

namespace DebtBot.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly DebtContext _debtContext;

    public SubscriptionRepository(DebtContext debtContext)
    {
        _debtContext = debtContext;
    }

    public void Add(Guid userId, Guid contactId, bool subscriptionStatus = false)
    {
        var subscription = _debtContext.NotificationSubscriptions.FirstOrDefault(t => t.UserId == contactId && t.SubscriberId == userId);
        if (subscription == null)
        {
            subscription = new NotificationSubscription()
            {
                UserId = contactId,
                SubscriberId = userId,
                IsConfirmed = subscriptionStatus
            };

            _debtContext.NotificationSubscriptions.Add(subscription);

            _debtContext.SaveChanges();
        }
    }

    public void SetSubscriptionStatusConfirmed(Guid subscriberId, Guid subscriptionTargetId)
    {
        var subscription = _debtContext.NotificationSubscriptions.FirstOrDefault(t => t.SubscriberId == subscriberId && t.UserId == subscriptionTargetId);
        if (subscription != null)
        {
            subscription.IsConfirmed = true;

            _debtContext.SaveChanges();
        }
    }

    public void RemoveSubscription(Guid subscriberId, Guid subscriptionTargetId)
    {
        var subscription = _debtContext.NotificationSubscriptions.FirstOrDefault(t => t.SubscriberId == subscriberId && t.UserId == subscriptionTargetId);
        if (subscription != null)
        {
            _debtContext.NotificationSubscriptions.Remove(subscription);

            _debtContext.SaveChanges();
        }
    }
}