using DebtBot.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DebtBot.Models.Enums;
using DebtBot.Interfaces.Services;
using DebtBot.Models.User;

namespace DebtBot.Controllers;

#if DEBUG

[ApiController]
[Route("api/v1/[controller]")]
public class IdentityController : ControllerBase
{
    private readonly IUserService _userService;

    public IdentityController(IOptions<DebtBotConfiguration> debtBotConfig, IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var userModel = _userService.GetUserById(id);
        if (userModel is null)
        {
            return NotFound();
        }
        return GenerateUserJwt(userModel);
    }
    
    [HttpGet]
    public IActionResult Get()
    {
        var userModel = _userService.GetFirstAdmin() ?? _userService.CreateAdmin();
        return GenerateUserJwt(userModel);
    }

    private IActionResult GenerateUserJwt(UserModel userModel)
    {
        return Ok(_userService.GenerateJwtToken(userModel));
    }
}

#endif
