using DebtBot.Models;
using DebtBot.ServiceInterfaces;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : ControllerBase
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
    public ActionResult Post(UserModel user)
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
}