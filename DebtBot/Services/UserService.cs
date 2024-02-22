using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Services;

public class UserService : IUserService
{
    private readonly DebtContext _debtContext;
    private readonly IMapper _mapper;

    public UserService(DebtContext db, IMapper mapper)
    {
        _debtContext = db;
        _mapper = mapper;
    }

    public IEnumerable<UserModel> GetUsers()
    {
        var users = _debtContext.Users.ProjectTo<UserModel>(_mapper.ConfigurationProvider).ToList();
        return users;
    }

    public UserModel? GetUserById(Guid id)
    {
        var user = _debtContext.Users.Where(u => u.Id == id).ProjectTo<UserModel>(_mapper.ConfigurationProvider).FirstOrDefault();
        return user;
    }

    public UserModel AddUser(UserCreationModel user)
    {
        var entity = _mapper.Map<User>(user);
        _debtContext.Users.Add(entity);
        _debtContext.SaveChanges();

        return _mapper.Map<UserModel>(entity);
    }

    public void UpdateUser(UserModel user)
    {
        var entity = _mapper.Map<User>(user);
        _debtContext.Users.Update(entity);
        _debtContext.SaveChanges();
    }

    public bool SetRole(Guid id, UserRole role)
    {
        var user = _debtContext.Users.FirstOrDefault(q => q.Id == id);
        if (user is null)
        {
            return false;
        }

        user.Role = role;
        _debtContext.SaveChanges();
        return true;
    }

    public void DeleteUser(Guid id)
    {
        var user = _debtContext.Users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            _debtContext.Users.Remove(user);
            _debtContext.SaveChanges();
        }
    }

    public UserModel GetFirstAdmin()
    {

        var user = _debtContext.Users.FirstOrDefault(q => q.Role == UserRole.Admin);
        if (user is null)
        {
            user = new()
            {
                DisplayName = "Admin",
                Role = UserRole.Admin
            };

            _debtContext.Users.Add(user);
            _debtContext.SaveChanges();
        }

        return _mapper.Map<UserModel>(user);
    }

    public void MergeUsers(Guid oldUserId, Guid newUserId)
    {
        var oldUser = _debtContext.Users.FirstOrDefault(u => u.Id == oldUserId);
        var newUser = _debtContext.Users.FirstOrDefault(u => u.Id == newUserId);

        if (oldUser is null || newUser is null)
        {
            return;
        }

        using var transaction = _debtContext.Database.BeginTransaction();
        

        // Updates LedgerRecords where oldUser is creditor
        
        _debtContext.LedgerRecords
            .Where(q => q.CreditorUserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.CreditorUserId, newUserId));
        
        // Updates LedgerRecords where oldUser is debtor

        _debtContext.LedgerRecords
            .Where(q => q.DebtorUserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.DebtorUserId, newUserId));

        // User contacts of oldUser

        _debtContext.UserContactsLinks
            .Where(t => t.UserId == oldUserId
                && !_debtContext.UserContactsLinks
                    .Any(c => c.UserId == newUserId && c.ContactUserId == t.ContactUserId))
            .ExecuteUpdate(t => t.SetProperty(t => t.UserId, newUserId));

        _debtContext.UserContactsLinks
            .Where(t => t.UserId == oldUserId)
            .ExecuteDelete();

        // User contacts with oldUser

        _debtContext.UserContactsLinks
            .Where(t => t.ContactUserId == oldUserId
                && !_debtContext.UserContactsLinks
                    .Any(c => c.ContactUserId == newUserId && c.UserId == t.UserId))
            .ExecuteUpdate(t => t.SetProperty(t => t.ContactUserId, newUserId));

        _debtContext.UserContactsLinks
            .Where(t => t.ContactUserId == oldUserId)
            .ExecuteDelete();
        
        // Budgets of oldUser

        _debtContext.Spendings
            .Where(q => q.UserId == oldUserId)
            .ExecuteUpdate(s => s.SetProperty(p => p.UserId, newUserId));

        _debtContext.Users.Remove(oldUser);

        _debtContext.SaveChanges();

        transaction.Commit();
    }

    public void ActualizeTelegramUser(long telegramId, string? telegramUserName, string firstName, string? lastName)
    {
        var username = telegramUserName is null ? null : $"@{telegramUserName}";

        var displayName = string.Join(" ", new[] { firstName, lastName }.Where(t => !string.IsNullOrWhiteSpace(t)));

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = string.IsNullOrEmpty(username) ? telegramId.ToString() : username;

        var user = _debtContext.Users.FirstOrDefault(u => u.TelegramId == telegramId);
        if (user is null && !string.IsNullOrEmpty(username))
        {
            user = _debtContext.Users.FirstOrDefault(u => u.TelegramUserName == username);
        }

        if (user is null)
        {
            user = new()
            {
                TelegramId = telegramId,
                TelegramUserName = username,
                DisplayName = displayName
            };
            _debtContext.Users.Add(user);
        }
        else
        {
            user.TelegramId = telegramId;
            user.TelegramUserName = username;
            user.DisplayName = displayName;
        }

        _debtContext.SaveChanges();
    }

    public UserModel? FindUser(UserSearchModel? model, Guid? userId = null) 
    {
        User? user = null;

        if (model.Id is not null)
        {
            user = _debtContext.Users.FirstOrDefault(u => u.Id == model.Id);
        }
        if (user is null && model.TelegramId is not null)
        {
            user = _debtContext.Users.FirstOrDefault(u => u.TelegramId == model.TelegramId);
        }
        if (user is null && !string.IsNullOrEmpty(model.TelegramUserName))
        {
            user = _debtContext.Users.FirstOrDefault(u => u.TelegramUserName == model.TelegramUserName);
        }
        if (user is null && !string.IsNullOrEmpty(model.Phone))
        {
            user = _debtContext.Users.FirstOrDefault(u => u.Phone == model.Phone);
        }
        if (user is null && !string.IsNullOrEmpty(model.Email))
        {
            user = _debtContext.Users.FirstOrDefault(u => u.Email == model.Email);
        }
        if (user is null && !string.IsNullOrEmpty(model.QueryString))
        {
            user = _debtContext.Users.FirstOrDefault(u => u.TelegramId.ToString() == model.QueryString);
            user ??= _debtContext.Users.FirstOrDefault(u => u.TelegramUserName == model.QueryString);
            user ??= _debtContext.Users.FirstOrDefault(u => u.Phone == model.QueryString);
            user ??= _debtContext.Users.FirstOrDefault(u => u.Email == model.QueryString);
        }

        var searchString = model.DisplayName ?? model.QueryString;
        if (user is null 
            && userId is not null
            && !string.IsNullOrEmpty(searchString))
        {
            user = _debtContext.UserContactsLinks
                .Where(t => t.UserId == userId && t.DisplayName == searchString)
                .Select(t => t.ContactUser)
                .FirstOrDefault();
        }

        if (user is null)
            return null;

        return _mapper.Map<UserModel>(user);
    }

    public UserModel AddUser(UserSearchModel model)
    {
        var entity = _mapper.Map<User>(model);
        _debtContext.Users.Add(entity);
        _debtContext.SaveChanges();

        return _mapper.Map<UserModel>(entity); 
    }
}