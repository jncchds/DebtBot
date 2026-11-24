using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Profile;
using DebtBot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

/// <summary>The signed-in user's own profile. The user always comes from the token.</summary>
[Authorize]
[ApiController]
[Route("api/v1/profile")]
public class ProfileController : DebtBotControllerBase
{
    private readonly IUserService _userService;
    private readonly IApiTokenService _apiTokens;

    public ProfileController(IUserService userService, IApiTokenService apiTokens)
    {
        _userService = userService;
        _apiTokens = apiTokens;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var profile = _userService.GetProfile(UserId!.Value);
        if (profile is null)
        {
            return ErrorResult(StatusCodes.Status404NotFound, "User not found.");
        }

        return Ok(profile);
    }

    /// <summary>Replaces display name, phone and email. Returns the profile.</summary>
    [HttpPut]
    public IActionResult Put(ProfileUpdateModel model)
    {
        var errors = _userService.UpdateProfile(UserId!.Value, model);
        if (errors.Count != 0)
        {
            return ErrorResult(StatusCodes.Status400BadRequest, errors.ToArray());
        }

        return Ok(_userService.GetProfile(UserId!.Value));
    }

    /// <summary>Your API tokens (never the secrets): active first, then expired, then revoked. Not with an API token.</summary>
    [JwtOnly]
    [HttpGet("tokens")]
    public ActionResult<List<ApiTokenModel>> GetTokens()
    {
        return _apiTokens.List(UserId!.Value);
    }

    /// <summary>
    /// Makes an API token: <c>{ name, scope: Read|ReadWrite, expiresInDays: 30|90|365|null }</c>. The answer carries
    /// <c>token</c>, the secret, this once only. 409 at the limit of active tokens. Not with an API token.
    /// </summary>
    [JwtOnly]
    [HttpPost("tokens")]
    public async Task<IActionResult> CreateToken(ApiTokenCreationModel model, CancellationToken cancellationToken)
    {
        var result = await _apiTokens.CreateAsync(UserId!.Value, model, cancellationToken);
        return result.Status switch
        {
            ApiTokenCreateStatus.Created => Ok(result.Token),
            ApiTokenCreateStatus.LimitReached => ErrorResult(StatusCodes.Status409Conflict, result.Error!),
            _ => ErrorResult(StatusCodes.Status400BadRequest, result.Error!),
        };
    }

    /// <summary>Revokes one of your API tokens (204, also when it was already revoked; 404 when it isn't yours). Not with an API token.</summary>
    [JwtOnly]
    [HttpDelete("tokens/{id:guid}")]
    public async Task<IActionResult> RevokeToken(Guid id, CancellationToken cancellationToken)
    {
        return await _apiTokens.RevokeAsync(UserId!.Value, id, cancellationToken)
            ? NoContent()
            : ErrorResult(StatusCodes.Status404NotFound, "Token not found.");
    }
}
