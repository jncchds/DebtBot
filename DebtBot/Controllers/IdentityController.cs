using DebtBot.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DebtBot.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class IdentityController : ControllerBase
{
    private readonly JwtConfiguration _jwtConfig;

    public IdentityController(IOptions<DebtBotConfiguration> debtBotConfig)
    {
        _jwtConfig = debtBotConfig.Value.JwtConfiguration;
    }

    [HttpGet("{id}")]
    public IActionResult Get(Guid? id)
    {
        // db code here:
        // true

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, "DebtBot"),
            new Claim(IdentityData.AdminUserClaimName, "true")
        };

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
