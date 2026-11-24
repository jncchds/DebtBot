using DebtBot.Models.User;

namespace DebtBot.Interfaces;
public interface IIdentityService
{
    string GenerateJwtToken(UserModel userModel);
    AccessToken GenerateAccessToken(UserModel userModel);
}

public record AccessToken(string Token, DateTime ExpiresAt);