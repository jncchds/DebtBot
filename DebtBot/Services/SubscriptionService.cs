using AutoMapper;
using DebtBot.Interfaces.Services;
using DebtBot.Repositories;

namespace DebtBot.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SubscriptionService(ISubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public void RequestSubscription(Guid userId, Guid contactId, bool forceSubscribe = false)
    {
        _subscriptionRepository.Add(userId, contactId, forceSubscribe);
    }

    public void ConfirmSubscription(Guid subscriberId, Guid subscriptionTargetId)
    {
        _subscriptionRepository.SetSubscriptionStatusConfirmed(subscriberId, subscriptionTargetId);
    }

    public void RemoveSubscription(Guid subscriberId, Guid subscriptionTargetId)
    {
        _subscriptionRepository.RemoveSubscription(subscriberId, subscriptionTargetId);
    }
}