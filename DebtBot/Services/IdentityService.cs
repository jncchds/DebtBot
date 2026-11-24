using DebtBot.Identity;
using DebtBot.Interfaces;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DebtBot.Services;

public class IdentityService : IIdentityService
{
    private readonly JwtConfiguration _jwtConfig;

    public IdentityService(IOptions<DebtBotConfiguration> debtBotConfig)
    {
        _jwtConfig = debtBotConfig.Value.JwtConfiguration;
    }

    public string GenerateJwtToken(UserModel userModel) => GenerateAccessToken(userModel).Token;

    public AccessToken GenerateAccessToken(UserModel userModel)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userModel.Id.ToString()),
            new Claim(ClaimTypes.Name, userModel.DisplayName),
        };

        if (userModel.Role == UserRole.Admin)
        {
            claims.Add(new Claim(IdentityData.AdminUserClaimName, "true"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtConfig.LifeTime > 0 ? _jwtConfig.LifeTime : 60);
        var token = new JwtSecurityToken(
            _jwtConfig.Issuer,
            _jwtConfig.Audience,
            claims,
            expires: expiresAt,
            signingCredentials: creds);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

}
