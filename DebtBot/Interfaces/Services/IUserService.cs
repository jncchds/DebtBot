using DebtBot.Models.Enums;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IUserService
{
    UserModel AddUser(UserCreationModel user);
    void DeleteUser(Guid id);
    UserModel? GetUserById(Guid id);
    UserDisplayModel? GetUserDisplayModelById(Guid id);
    IEnumerable<UserModel> GetUsers();
    void UpdateUser(Guid id, UserCreationModel user);
    bool SetRole(Guid id, UserRole role);
    UserModel? GetFirstAdmin();
    UserModel CreateAdmin();
    void MergeUsers(Guid oldUserId, Guid newUserId);
    void ActualizeTelegramUser(long telegramId, string? userName, string firstName, string? lastName);
    UserModel? FindUser(UserSearchModel model, Guid? userId = null);
    void SetBotActiveState(Guid userId, bool stateToSet);
    UserModel FindOrAddUser(UserSearchModel model, UserModel? owner = null);
    string GenerateJwtToken(UserModel userModel);
}