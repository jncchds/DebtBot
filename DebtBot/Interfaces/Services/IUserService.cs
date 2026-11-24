using DebtBot.Models;
using DebtBot.Models.Enums;
using DebtBot.Models.Profile;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public enum MergeUsersResult
{
    Merged,
    SameUser,
    NotFound
}

public enum DeleteUserResult
{
    Deleted,
    NotFound,
    /// <summary>The user still has bills, ledger records or spendings, or is in someone else's contacts; merge them into another user instead.</summary>
    InUse,
    /// <summary>The user has the Admin role: make them Regular first.</summary>
    IsAdmin
}

public interface IUserService
{
    UserModel AddUser(UserCreationModel user);
    DeleteUserResult DeleteUser(Guid id);
    UserModel? GetUserById(Guid id);
    UserDisplayModel? GetUserDisplayModelById(Guid id);
    IEnumerable<UserModel> GetUsers();
    void UpdateUser(Guid id, UserCreationModel user);
    bool SetRole(Guid id, UserRole role);
    UserModel? GetFirstAdmin();
    UserModel CreateAdmin();
    MergeUsersResult MergeUsers(Guid oldUserId, Guid newUserId);
    ProfileModel? GetProfile(Guid id);
    /// <summary>Replaces display name, phone and email. Returns the validation errors; empty on success.</summary>
    List<string> UpdateProfile(Guid id, ProfileUpdateModel model);
    PagingResult<AdminUserModel> GetAdminUsers(string? search, UserRole? role, int pageNumber = 0, int? countPerPage = null);
    AdminUserModel? GetAdminUser(Guid id);
    List<MergeSuggestionModel> GetMergeSuggestions();
    void ActualizeTelegramUser(long telegramId, string? userName, string firstName, string? lastName);
    UserModel? FindUser(UserSearchModel model, Guid? userId = null);
    void SetBotActiveState(Guid userId, bool stateToSet);
    UserModel FindOrAddUser(UserSearchModel model, UserModel? owner = null);
}