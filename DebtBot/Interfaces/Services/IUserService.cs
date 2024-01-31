using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IUserService
{
    void AddUser(UserCreationModel user);
    void DeleteUser(Guid id);
    UserModel? GetUserById(Guid id);
    IEnumerable<UserModel> GetUsers();
    void UpdateUser(UserModel user);
}