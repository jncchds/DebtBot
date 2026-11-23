using DebtBot.Interfaces.Services;
using DebtBot.Models.Profile;
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

    public ProfileController(IUserService userService)
    {
        _userService = userService;
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
}
