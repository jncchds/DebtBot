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
    private readonly IUserService _userService;

    public UserContactsController(IUserService userService)
    {
        _userService = userService;
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpGet()]
    public IActionResult Get()
    {
        return Ok(_userService.GetContacts());
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var contactLinks = _userService.GetContacts(id);
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
        var contactLinks = _userService.GetContacts(UserId!.Value);
        if (contactLinks?.Any() ?? false)
        {
            return Ok(contactLinks);
        }

        return NotFound();
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpPost("{id}")]
    public IActionResult Post(Guid id, Guid contactUserId, string displayName)
    {
        _userService.AddContact(id, contactUserId, displayName);

        return Ok();
    }

    [Authorize]
    [HttpPost]
    public IActionResult Post(Guid contactUserId, string displayName)
    {
        _userService.AddContact(UserId!.Value, contactUserId, displayName);

        return Ok();
    }
}