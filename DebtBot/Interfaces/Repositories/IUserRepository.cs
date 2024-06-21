using DebtBot.Models.Enums;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Repositories;
public interface IUserRepository
{
    void AddContact(Guid id, Guid contactId, string DisplayName);
    UserModel AddUser(UserCreationModel user);
    UserModel CreateAdmin();
    bool DeleteUser(Guid id);
    UserModel? FindUser(UserSearchModel model, Guid? userId = null);
    IEnumerable<UserModel> GetContacts();
    IEnumerable<UserModel> GetContacts(Guid id);
    UserModel? GetFirstAdmin();
    UserModel? GetUserById(Guid id);
    UserDisplayModel? GetUserDisplayModelById(Guid id);
    IEnumerable<UserModel> GetUsers();
    void MergeUsers(Guid oldUserId, Guid newUserId);
    bool SetBotActiveState(Guid userId, bool stateToSet);
    bool SetRole(Guid id, UserRole role);
    void UpdateUser(Guid id, UserCreationModel user);
}