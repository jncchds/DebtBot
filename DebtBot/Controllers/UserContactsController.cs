using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Contact;
using DebtBot.Models.User;
using DebtBot.Services;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public partial class UserContactsController : DebtBotControllerBase
{
    private readonly IUserContactService _userContactService;
    private readonly IUserService _userService;

    public UserContactsController(IUserContactService userContactService, IUserService userService)
    {
        _userContactService = userContactService;
        _userService = userService;
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

    /// <summary>My contacts with subscription states; an empty list when there are none.</summary>
    [Authorize]
    [HttpGet("Own")]
    public IActionResult GetOwn()
    {
        return Ok(_userContactService.GetContacts(UserId!.Value));
    }

    /// <summary>Renames one of my contacts. Returns the contact.</summary>
    [Authorize]
    [HttpPut("Own/{contactId:guid}")]
    public IActionResult RenameOwn(Guid contactId, ContactRenameModel model)
    {
        var name = model.DisplayName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > UserService.MaxDisplayNameLength)
        {
            return ErrorResult(StatusCodes.Status400BadRequest, $"A name of 1-{UserService.MaxDisplayNameLength} characters is required.");
        }

        if (!_userContactService.RenameContact(UserId!.Value, contactId, name))
        {
            return ErrorResult(StatusCodes.Status404NotFound, "Contact not found.");
        }

        return Ok(_userContactService.GetContact(UserId!.Value, contactId));
    }

    /// <summary>Removes one of my contacts. Bills and subscriptions stay; a later bill with them adds the contact back.</summary>
    [Authorize]
    [HttpDelete("Own/{contactId:guid}")]
    public IActionResult DeleteOwn(Guid contactId)
    {
        if (!_userContactService.RemoveContact(UserId!.Value, contactId))
        {
            return ErrorResult(StatusCodes.Status404NotFound, "Contact not found.");
        }

        return NoContent();
    }

    /// <summary>
    /// Adds a contact by Telegram username. Finds the user, or creates a placeholder user the way bills do
    /// for an unknown @mention. Returns the contact.
    /// </summary>
    [Authorize]
    [HttpPost("Own/by-telegram")]
    public IActionResult AddOwnByTelegram([FromQuery] string? username)
    {
        var name = username?.Trim().TrimStart('@');
        if (string.IsNullOrEmpty(name) || !TelegramUserNameRegex().IsMatch(name))
        {
            return ErrorResult(StatusCodes.Status400BadRequest, "A Telegram username is 5-32 characters: letters, digits or '_'.");
        }

        var owner = _userService.GetUserById(UserId!.Value);
        if (owner is null)
        {
            return ErrorResult(StatusCodes.Status404NotFound, "User not found.");
        }

        var mention = $"@{name}";
        var contactUser = _userService.FindOrAddUser(new UserSearchModel { TelegramUserName = mention, DisplayName = mention }, owner);
        if (contactUser.Id == owner.Id)
        {
            return ErrorResult(StatusCodes.Status400BadRequest, "You can't add yourself as a contact.");
        }

        _userContactService.EnsureContact(owner.Id, contactUser.Id, contactUser.DisplayName ?? mention);

        return Ok(_userContactService.GetContact(owner.Id, contactUser.Id));
    }

    [GeneratedRegex("^[A-Za-z0-9_]{5,32}$")]
    private static partial Regex TelegramUserNameRegex();

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