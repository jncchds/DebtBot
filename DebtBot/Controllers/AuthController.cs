using DebtBot.Interfaces.Services;
using DebtBot.Models.Auth;
using DebtBot.Services;
using DebtBot.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DebtBot.Controllers;

/// <summary>
/// Web sign-in. A session is a short-lived JWT access token (kept in the SPA's memory and sent as
/// <c>Authorization: Bearer</c>) plus a long-lived refresh token in an httpOnly cookie scoped to <c>/api/v1/auth</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/auth")]
public class AuthController : DebtBotControllerBase
{
    public const string RefreshCookieName = "debtbot_refresh";
    public const string RefreshCookiePath = "/api/v1/auth";
    public const string RateLimitPolicy = "auth";

    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly TelegramBotInfoProvider _botInfo;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthService authService, IUserService userService, TelegramBotInfoProvider botInfo, IWebHostEnvironment environment)
    {
        _authService = authService;
        _userService = userService;
        _botInfo = botInfo;
        _environment = environment;
    }

    /// <summary>What the sign-in page needs: the bot username for the Telegram Login Widget, or null to hide it.</summary>
    [AllowAnonymous]
    [HttpGet("config")]
    public async Task<ActionResult<AuthConfigModel>> GetConfig(CancellationToken cancellationToken)
    {
        return new AuthConfigModel
        {
            TelegramBotUsername = await _botInfo.GetBotUsernameAsync(cancellationToken),
        };
    }

    /// <summary>Signs in with the data from the Telegram Login Widget.</summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    [HttpPost("telegram")]
    public async Task<ActionResult<SessionModel>> TelegramSignIn(TelegramLoginModel model, CancellationToken cancellationToken)
    {
        var result = await _authService.SignInWithTelegramAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = result.Error });
        }

        return await StartSessionAsync(result.User!, cancellationToken);
    }

    /// <summary>Signs in with a login and password.</summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    [HttpPost("login")]
    public async Task<ActionResult<SessionModel>> Login(LoginModel model, CancellationToken cancellationToken)
    {
        var result = await _authService.SignInWithPasswordAsync(model.Login, model.Password, cancellationToken);
        if (result.LockedOut)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = result.Error });
        }
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = AuthService.InvalidCredentialsMessage });
        }

        return await StartSessionAsync(result.User!, cancellationToken);
    }

    /// <summary>Exchanges the refresh cookie for a new access token and rotates the cookie.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<SessionModel>> Refresh(CancellationToken cancellationToken)
    {
        var raw = Request.Cookies[RefreshCookieName];
        var result = string.IsNullOrEmpty(raw) ? null : await _authService.RefreshAsync(raw, cancellationToken);
        if (result is null)
        {
            ClearRefreshCookie();
            return Unauthorized(new { message = "Not signed in." });
        }

        SetRefreshCookie(result.RefreshToken);
        return _authService.CreateSession(result.User);
    }

    /// <summary>Ends this browser's session. Anonymous on purpose: the access token may already have expired.</summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var raw = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(raw))
        {
            await _authService.RevokeRefreshTokenAsync(raw, cancellationToken);
        }
        ClearRefreshCookie();
        return NoContent();
    }

    /// <summary>The signed-in user.</summary>
    [HttpGet("me")]
    public ActionResult<SessionUserModel> Me()
    {
        var user = _userService.GetUserById(UserId!.Value);
        if (user is null)
        {
            return Unauthorized();
        }

        return _authService.CreateSession(user).User;
    }

    /// <summary>
    /// Sets the current user's login and password the first time, or changes them (which needs the current password).
    /// Changing the password signs out the user's other web sessions.
    /// </summary>
    [HttpPut("password")]
    public async Task<IActionResult> SetPassword(SetPasswordModel model, CancellationToken cancellationToken)
    {
        var result = await _authService.SetOwnPasswordAsync(UserId!.Value, model, Request.Cookies[RefreshCookieName], cancellationToken);
        return result.ToActionResult(this);
    }

    private async Task<SessionModel> StartSessionAsync(Models.User.UserModel user, CancellationToken cancellationToken)
    {
        var refreshToken = await _authService.IssueRefreshTokenAsync(user.Id, cancellationToken);
        SetRefreshCookie(refreshToken);
        return _authService.CreateSession(user);
    }

    private void SetRefreshCookie(IssuedRefreshToken token) =>
        Response.Cookies.Append(RefreshCookieName, token.Token, CookieOptions(token.ExpiresAt));

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, CookieOptions(null));

    private CookieOptions CookieOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = !_environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        Expires = expires,
        IsEssential = true,
    };
}

public static class PasswordChangeResultExtensions
{
    public static IActionResult ToActionResult(this PasswordChangeResult result, ControllerBase controller) => result switch
    {
        PasswordChangeResult.Success => controller.NoContent(),
        PasswordChangeResult.UserNotFound => controller.NotFound(new { message = "User not found." }),
        PasswordChangeResult.LoginTaken => controller.Conflict(new { message = "This login is already taken." }),
        PasswordChangeResult.LoginRequired => controller.BadRequest(new { message = "A login is required." }),
        PasswordChangeResult.InvalidLogin => controller.BadRequest(new
        {
            message = $"A login is {AuthService.MinLoginLength}-{AuthService.MaxLoginLength} characters: letters, digits, '.', '_', '-' or '@'."
        }),
        PasswordChangeResult.PasswordRequired => controller.BadRequest(new { message = "A new password is required." }),
        PasswordChangeResult.PasswordTooShort => controller.BadRequest(new
        {
            message = $"The password must be at least {AuthService.MinPasswordLength} characters."
        }),
        PasswordChangeResult.CurrentPasswordRequired => controller.BadRequest(new { message = "The current password is required." }),
        PasswordChangeResult.CurrentPasswordIncorrect => controller.BadRequest(new { message = "The current password is incorrect." }),
        _ => controller.BadRequest(),
    };
}
