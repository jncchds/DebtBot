using DebtBot.Models;

namespace DebtBot.Interfaces.Services;

public interface IUserService
{
    void AddUser(UserModel user);
    void DeleteUser(Guid id);
    UserModel? GetUserById(Guid id);
    IEnumerable<UserModel> GetUsers();
    void UpdateUser(UserModel user);
}