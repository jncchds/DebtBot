using DebtBot.Models.Enums;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IUserService
{
    void AddUser(UserCreationModel user);
    void DeleteUser(Guid id);
    UserModel? GetUserById(Guid id);
    IEnumerable<UserModel> GetUsers();
    void UpdateUser(UserModel user);
    bool SetRole(Guid id, UserRole role);
    public UserModel GetFirstAdmin();
    void MergeUsers(Guid oldUserId, Guid newUserId);
}