using DebtBot.Interfaces;
using DebtBot.Interfaces.Services;
using DebtBot.Models.User;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DebtBot.Controllers;

#if DEBUG

[ApiController]
[Route("api/v1/[controller]")]
public class IdentityController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IIdentityService _identityService;

    public IdentityController(IOptions<DebtBotConfiguration> debtBotConfig, IUserService userService, IIdentityService identityService)
    {
        _userService = userService;
        _identityService = identityService;
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
        return Ok(_identityService.GenerateJwtToken(userModel));
    }
}

#endif
