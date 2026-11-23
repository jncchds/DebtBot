using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.Enums;
using DebtBot.Models.Profile;
using DebtBot.Models.User;

namespace DebtBot.Repositories;
public interface IUserRepository
{
    UserModel AddUser(UserCreationModel user);
    UserModel CreateAdmin();
    DeleteUserResult DeleteUser(Guid id);
    UserModel? FindUser(UserSearchModel model, Guid? userId = null);
    UserModel? GetFirstAdmin();
    UserModel? GetUserById(Guid id);
    UserDisplayModel? GetUserDisplayModelById(Guid id);
    IEnumerable<UserModel> GetUsers();
    bool MergeUsers(Guid oldUserId, Guid newUserId);
    ProfileModel? GetProfile(Guid id);
    bool UpdateProfile(Guid id, string displayName, string? phone, string? email);
    PagingResult<AdminUserModel> GetAdminUsers(string? search, UserRole? role, int pageNumber = 0, int? countPerPage = null);
    AdminUserModel? GetAdminUser(Guid id);
    List<AdminUserModel> GetAdminUsersWithTelegram();
    bool SetBotActiveState(Guid userId, bool stateToSet);
    bool SetRole(Guid id, UserRole role);
    void UpdateUser(Guid id, UserCreationModel user);
}