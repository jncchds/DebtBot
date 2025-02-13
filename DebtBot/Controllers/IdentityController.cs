using DebtBot.Identity;
using DebtBot.Interfaces;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.User;
using DebtBot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using Telegram.Bot.Extensions.LoginWidget;

namespace DebtBot.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class IdentityController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IIdentityService _identityService;
    private readonly LoginWidget _loginWidget;
    private readonly ITelegramService _telegramService;

    public IdentityController(
        IOptions<DebtBotConfiguration> debtBotConfig,
        IUserService userService,
        IIdentityService identityService,
        LoginWidget loginWidget,
        ITelegramService telegramService)
    {
        _userService = userService;
        _identityService = identityService;
        _loginWidget = loginWidget;
        _telegramService = telegramService;
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
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

    [HttpPost("Telegram")]
    public IActionResult PostTelegramAuth(TelegramAuthData authData)
    {
        var sortedDict = new SortedDictionary<string, string>
        {
            { "id", authData.Id.ToString(CultureInfo.InvariantCulture) },
            { "first_name", authData.FirstName },
            { "last_name", authData.LastName },
            { "username", authData.Username },
            { "photo_url", authData.PhotoUrl },
            { "auth_date", authData.AuthDate.ToString(CultureInfo.InvariantCulture) },
            { "hash", authData.Hash }
        };

        var auth = _loginWidget.CheckAuthorization(sortedDict);

        if (!auth.HasFlag(Authorization.Valid))
        {
            return Unauthorized();
        }

        var user = _telegramService.GetUserByTelegramId(authData.Id);
        if (user is null)
        {
            return Unauthorized();
        }

        return Get(user.Value);
    }


#if DEBUG
    [HttpGet]
    public IActionResult Get()
    {
        var userModel = _userService.GetFirstAdmin() ?? _userService.CreateAdmin();
        return GenerateUserJwt(userModel);
    }
#endif

    private IActionResult GenerateUserJwt(UserModel userModel)
    {
        return Ok(_identityService.GenerateJwtToken(userModel));
    }
}

