using DebtBot.Models.User;

namespace DebtBot.Interfaces;
public interface IIdentityService
{
    string GenerateJwtToken(UserModel userModel);
}