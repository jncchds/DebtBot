using DebtBot.Models.Enums;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IUserService
{
    UserModel AddUser(UserCreationModel user);
    void DeleteUser(Guid id);
    UserModel? GetUserById(Guid id);
    IEnumerable<UserModel> GetUsers();
    void UpdateUser(UserModel user);
    bool SetRole(Guid id, UserRole role);
    public UserModel GetFirstAdmin();
    void MergeUsers(Guid oldUserId, Guid newUserId);
    void ActualizeTelegramUser(long telegramId, string? userName, string firstName, string? lastName);
    UserModel? FindUser(UserSearchModel model, Guid? userId = null);
    void SetBotActiveState(Guid userId, bool stateToSet);
    UserModel FindOrAddUser(UserSearchModel model, UserModel? owner = null);
    string GenerateJwtToken(UserModel userModel);
}