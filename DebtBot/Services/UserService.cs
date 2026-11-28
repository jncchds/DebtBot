using AutoMapper;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.Enums;
using DebtBot.Models.Profile;
using DebtBot.Models.User;
using DebtBot.Repositories;

namespace DebtBot.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IUserContactService _userContactService;

    public UserService(IUserRepository userRepository, IMapper mapper, IUserContactService userContactService)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _userContactService = userContactService;
    }

    public IEnumerable<UserModel> GetUsers()
    {
        return _userRepository.GetUsers();
    }

    public UserModel? GetUserById(Guid id)
    {
        return _userRepository.GetUserById(id);
    }

    public UserDisplayModel? GetUserDisplayModelById(Guid id)
    {
        return _userRepository.GetUserDisplayModelById(id);
    }

    public UserModel AddUser(UserCreationModel user)
    {
        return _userRepository.AddUser(user);
    }

    public void UpdateUser(Guid id, UserCreationModel user)
    {
        _userRepository.UpdateUser(id, user);
    }

    public bool SetRole(Guid id, UserRole role)
    {
        return _userRepository.SetRole(id, role);
    }

    public DeleteUserResult DeleteUser(Guid id)
    {
        return _userRepository.DeleteUser(id);
    }

    public UserModel? GetFirstAdmin()
    {
        return _userRepository.GetFirstAdmin();
    }

    public UserModel CreateAdmin()
    {
        return _userRepository.CreateAdmin();
    }

    public MergeUsersResult MergeUsers(Guid oldUserId, Guid newUserId)
    {
        if (oldUserId == newUserId)
        {
            return MergeUsersResult.SameUser;
        }

        return _userRepository.MergeUsers(oldUserId, newUserId)
            ? MergeUsersResult.Merged
            : MergeUsersResult.NotFound;
    }

    public ProfileModel? GetProfile(Guid id)
    {
        return _userRepository.GetProfile(id);
    }

    public const int MaxDisplayNameLength = 100;
    public const int MaxPhoneLength = 32;
    public const int MaxEmailLength = 254;

    public List<string> UpdateProfile(Guid id, ProfileUpdateModel model)
    {
        var errors = new List<string>();

        var displayName = model.DisplayName?.Trim();
        var phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        var email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();

        if (string.IsNullOrEmpty(displayName))
            errors.Add("A display name is required");
        else if (displayName.Length > MaxDisplayNameLength)
            errors.Add($"The display name can be at most {MaxDisplayNameLength} characters");
        if (phone is not null && phone.Length > MaxPhoneLength)
            errors.Add($"The phone can be at most {MaxPhoneLength} characters");
        if (email is not null && (email.Length > MaxEmailLength || !email.Contains('@')))
            errors.Add("The email doesn't look right");
        if (model.ReceiptOpenIn is not null && !Enum.IsDefined(model.ReceiptOpenIn.Value))
            errors.Add("Receipts open in Form or Text");

        if (errors.Count == 0 && !_userRepository.UpdateProfile(id, displayName!, phone, email, model.ReceiptOpenIn))
        {
            errors.Add("User not found");
        }

        return errors;
    }

    public PagingResult<AdminUserModel> GetAdminUsers(string? search, UserRole? role, int pageNumber = 0, int? countPerPage = null)
    {
        return _userRepository.GetAdminUsers(search, role, pageNumber, countPerPage);
    }

    public AdminUserModel? GetAdminUser(Guid id)
    {
        return _userRepository.GetAdminUser(id);
    }

    public List<MergeSuggestionModel> GetMergeSuggestions()
    {
        return MergeSuggestions.Build(_userRepository.GetAdminUsersWithTelegram());
    }

    public void ActualizeTelegramUser(long telegramId, string? telegramUserName, string firstName, string? lastName)
    {
        var displayName = string.Join(" ", new[] { firstName, lastName }.Where(t => !string.IsNullOrWhiteSpace(t)));
        var username = telegramUserName is null ? null : $"@{telegramUserName}";

        var user = _userRepository.FindUser(new()
        {
            TelegramId = telegramId,
            TelegramUserName = username,
            DisplayName = displayName
        });

        var updatedUser = new UserCreationModel
        {
            TelegramId = telegramId,
            TelegramUserName = username,
            DisplayName = displayName
        };

        if (user is null)
        {
            _userRepository.AddUser(updatedUser);
        }
        else
        {
            _userRepository.UpdateUser(user.Id, updatedUser);
        }
    }

    public UserModel? FindUser(UserSearchModel model, Guid? userId = null) 
    {
        return _userRepository.FindUser(model, userId);
    }

    public void SetBotActiveState(Guid userId, bool stateToSet)
    {
        _userRepository.SetBotActiveState(userId, stateToSet);
    }

    public UserModel FindOrAddUser(UserSearchModel model, UserModel? owner = null)
    {
        var user = FindUser(model, owner?.Id);
        if (user is not null)
            return user;

        var userCreationModel = _mapper.Map<UserCreationModel>(model);

        user = _userRepository.AddUser(userCreationModel);

        if (owner is not null)
        {
            _userContactService.AddContact(owner.Id, user);
            _userContactService.AddContact(user.Id, owner);
        }

        return user; 
    }
}