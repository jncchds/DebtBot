using DebtBot.Models.Enums;
using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Auth;
using DebtBot.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DebtBot.Controllers;

[Authorize(IdentityData.AdminUserPolicyName)]
[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : DebtBotControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;

    public UsersController(IUserService userService, IAuthService authService)
    {
       _userService = userService;
       _authService = authService;
    }

    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id)
    {
        var user = _userService.GetUserById(id);

        if (user == null)
            return ErrorResult(StatusCodes.Status404NotFound, "User not found.");

        return Ok(user);
    }

    /// <summary>
    /// The users list, ordered by display name. <paramref name="search"/> matches display name, Telegram username
    /// or login, ignoring case.
    /// </summary>
    [HttpGet()]
    public IActionResult Get(string? search = null, UserRole? role = null, int pageNumber = 0, int? countPerPage = null)
    {
        return Ok(_userService.GetAdminUsers(search, role, pageNumber, countPerPage));
    }

    /// <summary>Pairs of users that look like the same person, with the suggested user to keep.</summary>
    [HttpGet("merge/suggestions")]
    public IActionResult GetMergeSuggestions()
    {
        return Ok(_userService.GetMergeSuggestions());
    }

    [HttpPost()]
    public ActionResult Post(UserCreationModel user)
    {
        _userService.AddUser(user);

        return Ok();
    }

    /// <summary>
    /// Deletes a user that has no bills, ledger records, spendings or contacts (a stray placeholder).
    /// 409 when there is such data: merge the user into another one instead.
    /// </summary>
    [HttpDelete("{id}")]
    public IActionResult Delete(Guid id)
    {
        if (id == UserId)
        {
            return ErrorResult(StatusCodes.Status400BadRequest, "You can't delete the account you're signed in with.");
        }

        return _userService.DeleteUser(id) switch
        {
            DeleteUserResult.Deleted => Ok(),
            DeleteUserResult.InUse => ErrorResult(StatusCodes.Status409Conflict,
                "This user still has bills, debts or contacts. Merge them into another user instead."),
            _ => ErrorResult(StatusCodes.Status404NotFound, "User not found."),
        };
    }

    /// <summary>
    /// Updates a user's display name, Telegram username, phone and email. The Telegram id can't be changed:
    /// a different <c>telegramId</c> in the body gives 400.
    /// </summary>
    [HttpPut("{id}")]
    public IActionResult Put(Guid id, UserCreationModel user)
    {
        var existing = _userService.GetUserById(id);
        if (existing is null)
        {
            return ErrorResult(StatusCodes.Status404NotFound, "User not found.");
        }
        if (user.TelegramId is not null && user.TelegramId != existing.TelegramId)
        {
            return ErrorResult(StatusCodes.Status400BadRequest, "The Telegram id can't be changed.");
        }

        _userService.UpdateUser(id, user);

        return Ok();
    }

    [HttpPost("{id}/Role")]
    public ActionResult SetRole(Guid id, UserRole role)
    {
        var res = _userService.SetRole(id, role);
        return res? Ok(): NotFound();
    }

    /// <summary>
    /// Sets a user's web sign-in login and password. Login may be left empty when the user already has one.
    /// Signs the user out of all web sessions.
    /// </summary>
    [HttpPost("{id}/password")]
    public async Task<IActionResult> SetPassword(Guid id, AdminSetPasswordModel model, CancellationToken cancellationToken)
    {
        var result = await _authService.AdminSetPasswordAsync(id, model, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Merges <c>oldUserId</c> into <c>newUserId</c> and removes the old user. Takes a JSON body; the old query
    /// parameters still work. Returns the kept user's row.
    /// </summary>
    [HttpPost("merge")]
    public IActionResult Merge([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MergeUsersModel? model, [FromQuery] Guid? oldUserId = null, [FromQuery] Guid? newUserId = null)
    {
        var oldId = model is not null && model.OldUserId != Guid.Empty ? model.OldUserId : oldUserId ?? Guid.Empty;
        var newId = model is not null && model.NewUserId != Guid.Empty ? model.NewUserId : newUserId ?? Guid.Empty;

        if (oldId == Guid.Empty || newId == Guid.Empty)
        {
            return ErrorResult(StatusCodes.Status400BadRequest, "oldUserId and newUserId are required.");
        }
        if (oldId == UserId)
        {
            return ErrorResult(StatusCodes.Status400BadRequest, "You can't merge away the account you're signed in with.");
        }

        return _userService.MergeUsers(oldId, newId) switch
        {
            MergeUsersResult.Merged => Ok(_userService.GetAdminUser(newId)),
            MergeUsersResult.SameUser => ErrorResult(StatusCodes.Status400BadRequest, "Pick two different users."),
            _ => ErrorResult(StatusCodes.Status404NotFound, "User not found."),
        };
    }
}