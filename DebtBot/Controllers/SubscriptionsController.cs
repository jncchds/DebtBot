using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class SubscriptionsController : DebtBotControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [Authorize]
    [HttpPost("subscription/request")]
    public IActionResult RequestSubscription(Guid contactId)
    {
        _subscriptionService.RequestSubscription(UserId!.Value, contactId);

        return Ok();
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpPost("subscription/add")]
    public IActionResult AddSubscription(Guid userId, Guid contactId)
    {
        _subscriptionService.RequestSubscription(userId, contactId, true);

        return Ok();
    }

    [Authorize]
    [HttpPost("subscription/confirm")]
    public IActionResult ConfirmSubscription(Guid contactId)
    {
        _subscriptionService.ConfirmSubscription(UserId!.Value, contactId);

        return Ok();
    }

    [Authorize]
    [HttpPost("subscription/decline")]
    public IActionResult DeclineSubscription(Guid contactId)
    {
        _subscriptionService.RemoveSubscription(UserId!.Value, contactId);

        return Ok();
    }
}
