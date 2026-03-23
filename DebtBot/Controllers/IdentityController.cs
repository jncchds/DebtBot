using DebtBot.Interfaces;
using DebtBot.Interfaces.Services;
using DebtBot.Models.User;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace DebtBot.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class IdentityController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IIdentityService _identityService;
    private readonly DebtBotConfiguration _config;

    public IdentityController(IOptions<DebtBotConfiguration> debtBotConfig, IUserService userService, IIdentityService identityService)
    {
        _userService = userService;
        _identityService = identityService;
        _config = debtBotConfig.Value;
    }

    /// <summary>
    /// Validates a Telegram Login Widget callback and returns a JWT bearer token.
    /// </summary>
    [HttpPost("telegram-login")]
    public IActionResult TelegramLogin([FromBody] TelegramLoginModel model)
    {
        if (!ValidateTelegramHash(model))
            return Unauthorized("Invalid Telegram authentication data");

        var authAge = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - model.AuthDate;
        if (authAge > 86400)
            return Unauthorized("Authentication data is expired");

        var displayName = string.IsNullOrWhiteSpace(model.LastName)
            ? model.FirstName
            : $"{model.FirstName} {model.LastName}";

        var userSearch = new UserSearchModel
        {
            TelegramId = model.Id,
            TelegramUserName = model.Username,
            DisplayName = displayName
        };

        var user = _userService.FindOrAddUser(userSearch);
        _userService.ActualizeTelegramUser(model.Id, model.Username, model.FirstName, model.LastName);

        var userModel = _userService.GetUserById(user.Id);
        if (userModel is null)
            return NotFound();

        return Ok(_identityService.GenerateJwtToken(userModel));
    }

    private bool ValidateTelegramHash(TelegramLoginModel model)
    {
        var botToken = _config.Telegram.BotToken;
        if (string.IsNullOrWhiteSpace(botToken))
            return false;

        // Build the check string: all fields except hash, sorted alphabetically, joined with \n
        var fields = new SortedDictionary<string, string>
        {
            ["auth_date"] = model.AuthDate.ToString(),
            ["first_name"] = model.FirstName,
            ["id"] = model.Id.ToString()
        };
        if (!string.IsNullOrEmpty(model.LastName)) fields["last_name"] = model.LastName;
        if (!string.IsNullOrEmpty(model.Username)) fields["username"] = model.Username;
        if (!string.IsNullOrEmpty(model.PhotoUrl)) fields["photo_url"] = model.PhotoUrl;

        var checkString = string.Join("\n", fields.Select(kv => $"{kv.Key}={kv.Value}"));

        // Secret key is SHA256 of bot token
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));

        using var hmac = new HMACSHA256(secretKey);
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(checkString));
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        return computedHex == model.Hash;
    }

#if DEBUG
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
#endif
}

