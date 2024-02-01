using DebtBot.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DebtBot.DB.Enums;
using DebtBot.Interfaces.Services;
using DebtBot.Models.User;

namespace DebtBot.Controllers;

#if DEBUG

[ApiController]
[Route("api/v1/[controller]")]
public class IdentityController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly JwtConfiguration _jwtConfig;

    public IdentityController(IOptions<DebtBotConfiguration> debtBotConfig, IUserService userService)
    {
        _userService = userService;
        _jwtConfig = debtBotConfig.Value.JwtConfiguration;
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
        var userModel = _userService.GetFirstAdmin();
        return GenerateUserJwt(userModel);
    }

    private IActionResult GenerateUserJwt(UserModel userModel)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userModel.Id.ToString()),
            new Claim(ClaimTypes.Name, userModel.DisplayName),
        };

        if (userModel.Role == UserRole.Admin)
        {
            claims.Add( new Claim(IdentityData.AdminUserClaimName, "true") );
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _jwtConfig.Issuer,
            _jwtConfig.Audience,
            claims,
            expires: DateTime.Now.AddMinutes(_jwtConfig.LifeTime),
            signingCredentials: creds);

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token)
        });
    }
}

#endif
