using DebtBot.Interfaces.Services;
using DebtBot.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

/// <summary>
/// Non-admin user endpoints accessible by any authenticated user.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/users")]
public class UserSelfController : DebtBotControllerBase
{
    private readonly IUserService _userService;

    public UserSelfController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("own")]
    public ActionResult GetOwn()
    {
        var user = _userService.GetUserDisplayModelById(UserId!.Value);
        if (user == null)
            return NotFound();
        return Ok(user);
    }

    [HttpPut("own")]
    public ActionResult UpdateOwn(UserCreationModel user)
    {
        _userService.UpdateUser(UserId!.Value, user);
        return Ok();
    }

    [HttpGet("search")]
    public ActionResult Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Query string is required");
        var users = _userService.SearchUsers(q);
        return Ok(users);
    }
}
