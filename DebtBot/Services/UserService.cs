﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DebtBot.Services;

public class UserService : IUserService
{
    private readonly DebtContext _debtContext;
    private readonly IMapper _mapper;
    private readonly IUserContactService _userContactService;
    private readonly JwtConfiguration _jwtConfig;

    public UserService(DebtContext db, IMapper mapper, IUserContactService userContactService, IOptions<DebtBotConfiguration> debtBotConfig)
    {
        _debtContext = db;
        _mapper = mapper;
        _userContactService = userContactService;
        _jwtConfig = debtBotConfig.Value.JwtConfiguration;
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

    public void UpdateUser(Guid id, UserCreationModel user)
    {
        var entity = _debtContext.Users.FirstOrDefault(u => u.Id == id);
        if (entity is null)
        {
            return;
        }
        if (user.TelegramId != null)
        {
            entity.TelegramId = user.TelegramId.Value;
        }
        if (user.TelegramUserName != null)
        {
            entity.TelegramUserName = user.TelegramUserName;
        }
        if (user.DisplayName != null)
        {
            entity.DisplayName = user.DisplayName;
        }
        if (user.Email != null)
        {
            entity.Email = user.Email;
        }
        if (user.Phone != null)
        {
            entity.Phone = user.Phone;
        }

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

    public UserModel? GetFirstAdmin()
    {
        var user = _debtContext.Users.FirstOrDefault(q => q.Role == UserRole.Admin);

        if (user == null)
            return null;

        return _mapper.Map<UserModel>(user);
    }

    public UserModel CreateAdmin()
    {
        var user = new User()
        {
            DisplayName = "Admin",
            Role = UserRole.Admin
        };

        _debtContext.Users.Add(user);
        _debtContext.SaveChanges();


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
        
        // Updates Bills where oldUser is creator

        _debtContext.Bills
            .Where(q => q.CreatorId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.CreatorId, newUserId));

        // Updates BillPayments where oldUser is payer

        _debtContext.BillPayments
            .Where(q => q.UserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.UserId, newUserId));

        // Updates BillLineParticipants where oldUser is participant

        _debtContext.BillLineParticipants
            .Where(q => q.UserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.UserId, newUserId));

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

        //_debtContext.UserContactsLinks
        //    .Where(t => t.UserId == oldUserId)
        //    .ExecuteDelete();

        // User contacts with oldUser

        _debtContext.UserContactsLinks
            .Where(t => t.ContactUserId == oldUserId
                && !_debtContext.UserContactsLinks
                    .Any(c => c.ContactUserId == newUserId && c.UserId == t.UserId))
            .ExecuteUpdate(t => t.SetProperty(t => t.ContactUserId, newUserId));

        //_debtContext.UserContactsLinks
        //    .Where(t => t.ContactUserId == oldUserId)
        //    .ExecuteDelete();
        
        // Budgets of oldUser

        _debtContext.Spendings
            .Where(q => q.UserId == oldUserId)
            .ExecuteUpdate(s => s.SetProperty(p => p.UserId, newUserId));

        // Updates BillParticipants where oldUser is participant

        _debtContext.BillParticipants
            .Where(q => q.UserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.UserId, newUserId));

        // Updates NotificationSubscriptions both ways

        _debtContext.NotificationSubscriptions
            .Where(q => q.UserId == oldUserId
                && !_debtContext.NotificationSubscriptions
                    .Any(c => c.UserId == newUserId && c.SubscriberId == q.SubscriberId))
            .ExecuteUpdate(u => u.SetProperty(u => u.UserId, newUserId));

        //_debtContext.NotificationSubscriptions
        //    .Where(t => t.UserId == oldUserId)
        //    .ExecuteDelete();

        _debtContext.NotificationSubscriptions
            .Where(q => q.SubscriberId == oldUserId
                && !_debtContext.NotificationSubscriptions
                    .Any(c => c.SubscriberId == newUserId && c.UserId == q.UserId))
            .ExecuteUpdate(u => u.SetProperty(u => u.SubscriberId, newUserId));

        //_debtContext.NotificationSubscriptions
        //    .Where(t => t.SubscriberId == oldUserId)
        //    .ExecuteDelete();

        newUser.DisplayName = newUser.DisplayName ?? oldUser.DisplayName;
        newUser.TelegramId = newUser.TelegramId ?? oldUser.TelegramId;
        newUser.TelegramUserName = newUser.TelegramUserName ?? oldUser.TelegramUserName;
        newUser.Phone = newUser.Phone ?? oldUser.Phone;
        newUser.Email = newUser.Email ?? oldUser.Email;
        newUser.TelegramBotEnabled = newUser.TelegramBotEnabled || oldUser.TelegramBotEnabled;
        newUser.Role = oldUser.Role == UserRole.Admin ? UserRole.Admin : newUser.Role;

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

    public UserModel? FindUser(UserSearchModel model, Guid? userId = null) 
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
            user = _debtContext.Users.FirstOrDefault(u => EF.Functions.ILike(u.TelegramUserName!, model.TelegramUserName));
        }
        if (user is null && !string.IsNullOrEmpty(model.Phone))
        {
            user = _debtContext.Users.FirstOrDefault(u => EF.Functions.ILike(u.Phone!, model.Phone));
        }
        if (user is null && !string.IsNullOrEmpty(model.Email))
        {
            user = _debtContext.Users.FirstOrDefault(u => EF.Functions.ILike(u.Email!, model.Email));
        }
        if (user is null && !string.IsNullOrEmpty(model.QueryString))
        {
            user = _debtContext.Users.FirstOrDefault(u => u.TelegramId.ToString() == model.QueryString);
            user ??= _debtContext.Users.FirstOrDefault(u => EF.Functions.ILike(u.TelegramUserName ?? "", model.QueryString));
            user ??= _debtContext.Users.FirstOrDefault(u => EF.Functions.ILike(u.Phone ?? "", model.QueryString));
            user ??= _debtContext.Users.FirstOrDefault(u => EF.Functions.ILike(u.Email ?? "", model.QueryString));
        }

        var searchString = model.DisplayName ?? model.QueryString;
        if (user is null 
            && userId is not null
            && !string.IsNullOrEmpty(searchString))
        {
            user = _debtContext.UserContactsLinks
                .Where(t => t.UserId == userId && EF.Functions.ILike(t.DisplayName ?? "", searchString))
                .Select(t => t.ContactUser)
                .FirstOrDefault();
        }

        if (user is null)
            return null;

        return _mapper.Map<UserModel>(user);
    }

    public void SetBotActiveState(Guid userId, bool stateToSet)
    {
        _debtContext
            .Users
            .Where(u => u.Id == userId)
            .ExecuteUpdate(q => q.SetProperty(p => p.TelegramBotEnabled, stateToSet));
    }

    public UserModel FindOrAddUser(UserSearchModel model, UserModel? owner = null)
    {
        var lineUser = FindUser(model, owner?.Id);
        if (lineUser is not null)
            return lineUser;

        var entity = _mapper.Map<User>(model);
        _debtContext.Users.Add(entity);
        _debtContext.SaveChanges();

        lineUser = _mapper.Map<UserModel>(entity);

        if (owner is not null)
        {
            _userContactService.AddContact(owner.Id, lineUser);
            _userContactService.AddContact(lineUser.Id, owner);
        }

        return lineUser; 
    }

    public string GenerateJwtToken(UserModel userModel)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userModel.Id.ToString()),
            new Claim(ClaimTypes.Name, userModel.DisplayName),
        };

        if (userModel.Role == UserRole.Admin)
        {
            claims.Add(new Claim(IdentityData.AdminUserClaimName, "true"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _jwtConfig.Issuer,
            _jwtConfig.Audience,
            claims,
            expires: DateTime.Now.AddMinutes(_jwtConfig.LifeTime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}