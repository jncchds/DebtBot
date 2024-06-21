using AutoMapper;
using DebtBot.Interfaces.Repositories;
using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using MassTransit;

namespace DebtBot.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    private readonly IPublishEndpoint _publishEndpoint;

    public UserService(IUserRepository userRepository, IMapper mapper, IPublishEndpoint publishEndpoint)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
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

    public void DeleteUser(Guid id)
    {
        _userRepository.DeleteUser(id);
    }

    public UserModel? GetFirstAdmin()
    {
        return _userRepository.GetFirstAdmin();
    }

    public UserModel CreateAdmin()
    {
        return _userRepository.CreateAdmin();
    }

    public void MergeUsers(Guid oldUserId, Guid newUserId)
    {
        _userRepository.MergeUsers(oldUserId, newUserId);
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

        user = AddUser(userCreationModel);

        if (owner is not null)
        {
            _publishEndpoint.Publish(new EnsureContact(
                owner.Id,
                user.Id,
                user.DisplayName));
            _publishEndpoint.Publish(new EnsureContact(
                user.Id,
                owner.Id,
                owner.DisplayName));
        }

        return user;
    }

    public IEnumerable<UserModel> GetContacts()
    {
        return _userRepository.GetContacts();
    }

    public IEnumerable<UserModel> GetContacts(Guid id)
    {
        return _userRepository.GetContacts(id);
    }

    public void AddContact(Guid id, Guid contactId, string displayName)
    {
        _userRepository.AddContact(id, contactId, displayName);
    }
}