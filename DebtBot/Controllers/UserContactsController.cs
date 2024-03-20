using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class UserContactsController : DebtBotControllerBase
{
    private readonly IUserContactService _userContactService;

    public UserContactsController(IUserContactService userContactService)
    {
        _userContactService = userContactService;
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpGet()]
    public IActionResult Get()
    {
        return Ok(_userContactService.Get());
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var contactLinks = _userContactService.Get(id);
        if (contactLinks?.Any() ?? false)
        {
            return Ok(contactLinks);
        }

        return NotFound();
    }

    [Authorize]
    [HttpGet("Own")]
    public IActionResult GetOwn()
    {
        var contactLinks = _userContactService.Get(UserId!.Value);
        if (contactLinks?.Any() ?? false)
        {
            return Ok(contactLinks);
        }

        return NotFound();
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpPost("{id}")]
    public IActionResult Post(Guid id, UserModel contact)
    {
        _userContactService.AddContact(id, contact);

        return Ok();
    }

    [Authorize]
    [HttpPost]
    public IActionResult Post(UserModel contact)
    {
        _userContactService.AddContact(UserId!.Value, contact);

        return Ok();
    }

    [Authorize]
    [HttpPost("subscription/request")]
    public IActionResult RequestSubscription(Guid contactId)
    {
        _userContactService.RequestSubscription(UserId!.Value, contactId);

        return Ok();
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpPost("subscription/add")]
    public IActionResult AddSubscription(Guid userId, Guid contactId)
    {
        _userContactService.RequestSubscription(userId, contactId, true);

        return Ok();
    }

    [Authorize]
    [HttpPost("subscription/confirm")]
    public IActionResult ConfirmSubscription(Guid contactId)
    {
        _userContactService.ConfirmSubscription(UserId!.Value, contactId);

        return Ok();
    }

    [Authorize]
    [HttpPost("subscription/decline")]
    public IActionResult DeclineSubscription(Guid contactId)
    {
        _userContactService.DeclineSubscription(UserId!.Value, contactId);

        return Ok();
    }
}