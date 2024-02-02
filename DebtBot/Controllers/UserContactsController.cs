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
        var contactLinks = _userContactService.Get(UserId.Value);
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
        _userContactService.AddContact(UserId.Value, contact);

        return Ok();
    }
}