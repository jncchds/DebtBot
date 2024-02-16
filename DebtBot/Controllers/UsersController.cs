using DebtBot.Models.Enums;
using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[Authorize(IdentityData.AdminUserPolicyName)]
[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : DebtBotControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
       _userService = userService;
    }

    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var user = _userService.GetUserById(id);

        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpGet()]
    public IActionResult Get()
    {
        var users = _userService.GetUsers();

        if (users?.Any() ?? false)
            return Ok(users);

        return NotFound();
    }

    [HttpPost()]
    public ActionResult Post(UserCreationModel user)
    {
        _userService.AddUser(user);

        return Ok();
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(Guid id)
    {
        _userService.DeleteUser(id);

        return Ok();
    }

    [HttpPut()]
    public ActionResult Put(UserModel user)
    {
        _userService.UpdateUser(user);

        return Ok();
    }

    [HttpPost("{id}/Role")]
    public ActionResult SetRole(Guid id, UserRole role)
    {
        var res = _userService.SetRole(id, role);
        return res? Ok(): NotFound();
    }

    [HttpPost("merge")]
    public ActionResult Merge([FromQuery] Guid oldUserId, [FromQuery] Guid newUserId)
    {
        _userService.MergeUsers(oldUserId, newUserId);

        return Ok();
    }
}