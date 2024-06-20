using DebtBot.Models.Enums;
using DebtBot.Models.User;

namespace DebtBot.Repositories;
public interface IUserRepository
{
    UserModel AddUser(UserCreationModel user);
    UserModel CreateAdmin();
    bool DeleteUser(Guid id);
    UserModel? FindUser(UserSearchModel model, Guid? userId = null);
    UserModel? GetFirstAdmin();
    UserModel? GetUserById(Guid id);
    UserDisplayModel? GetUserDisplayModelById(Guid id);
    IEnumerable<UserModel> GetUsers();
    void MergeUsers(Guid oldUserId, Guid newUserId);
    bool SetBotActiveState(Guid userId, bool stateToSet);
    bool SetRole(Guid id, UserRole role);
    void UpdateUser(Guid id, UserCreationModel user);
}