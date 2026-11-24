using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.Interfaces.Services;
using DebtBot.DB.Entities;
using DebtBot.Extensions;
using DebtBot.Models;
using DebtBot.Models.Enums;
using DebtBot.Models.Profile;
using DebtBot.Models.User;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DebtContext _debtContext;
    private readonly IMapper _mapper;

    public UserRepository(DebtContext db, IMapper mapper)
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

    public UserDisplayModel? GetUserDisplayModelById(Guid id)
    {
        var user = _debtContext.Users.Where(u => u.Id == id).ProjectTo<UserDisplayModel>(_mapper.ConfigurationProvider).FirstOrDefault();
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

    public DeleteUserResult DeleteUser(Guid id)
    {
        var user = _debtContext.Users.FirstOrDefault(u => u.Id == id);
        if (user is null)
        {
            return DeleteUserResult.NotFound;
        }

        // An admin is never deleted, data or not: take the role away first.
        if (user.Role == UserRole.Admin)
        {
            return DeleteUserResult.IsAdmin;
        }

        // Ledger records and spendings have Restrict FKs, and deleting a bill's creator would cascade-delete the bill
        // for everyone in it. Users with any of this data, or in someone else's contacts, are merged into another user
        // instead. Their own contact list goes with them.
        var inUse = _debtContext.Bills.Any(b => b.CreatorId == id)
            || _debtContext.BillPayments.Any(p => p.UserId == id)
            || _debtContext.BillLineParticipants.Any(p => p.UserId == id)
            || _debtContext.BillParticipants.Any(p => p.UserId == id)
            || _debtContext.LedgerRecords.Any(l => l.CreditorUserId == id || l.DebtorUserId == id)
            || _debtContext.Spendings.Any(s => s.UserId == id)
            || _debtContext.UserContactsLinks.Any(c => c.ContactUserId == id);
        if (inUse)
        {
            return DeleteUserResult.InUse;
        }

        _debtContext.Users.Remove(user);
        _debtContext.SaveChanges();
        return DeleteUserResult.Deleted;
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

    /// <summary>
    /// Merges <paramref name="oldUserId"/> into <paramref name="newUserId"/> in one transaction and removes the old user.
    /// Rows keyed by user that would collide (both users in the same bill, line, contact or subscription) are combined
    /// into the kept user's row; rows that would point a user at itself (old and new as each other's contact,
    /// subscription or ledger counterpart) are deleted. The old user's refresh tokens and API tokens are deleted.
    /// </summary>
    /// <returns>False when either user doesn't exist.</returns>
    public bool MergeUsers(Guid oldUserId, Guid newUserId)
    {
        if (oldUserId == newUserId)
        {
            return false;
        }

        // The bulk updates below bypass the change tracker; entities tracked from before would be stale
        // (and removing the old user would try to cascade into them).
        _debtContext.ChangeTracker.Clear();

        using var transaction = _debtContext.Database.BeginTransaction();

        var oldUser = _debtContext.Users.FirstOrDefault(u => u.Id == oldUserId);
        var newUser = _debtContext.Users.FirstOrDefault(u => u.Id == newUserId);

        if (oldUser is null || newUser is null)
        {
            return false;
        }

        // Bills where oldUser is creator

        _debtContext.Bills
            .Where(q => q.CreatorId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.CreatorId, newUserId));

        // BillPayments: both paid in the same bill -> one payment with both amounts

        var paymentClashes = _debtContext.BillPayments
            .Where(q => q.UserId == oldUserId
                && _debtContext.BillPayments.Any(n => n.UserId == newUserId && n.BillId == q.BillId))
            .ToList();
        foreach (var clash in paymentClashes)
        {
            var kept = _debtContext.BillPayments.First(n => n.UserId == newUserId && n.BillId == clash.BillId);
            kept.Amount += clash.Amount;
            _debtContext.BillPayments.Remove(clash);
        }
        _debtContext.SaveChanges();

        _debtContext.BillPayments
            .Where(q => q.UserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.UserId, newUserId));

        // BillLineParticipants: both in the same line -> one participant with both parts

        var lineClashes = _debtContext.BillLineParticipants
            .Where(q => q.UserId == oldUserId
                && _debtContext.BillLineParticipants.Any(n => n.UserId == newUserId && n.BillLineId == q.BillLineId))
            .ToList();
        foreach (var clash in lineClashes)
        {
            var kept = _debtContext.BillLineParticipants.First(n => n.UserId == newUserId && n.BillLineId == clash.BillLineId);
            kept.Part += clash.Part;
            _debtContext.BillLineParticipants.Remove(clash);
        }
        _debtContext.SaveChanges();

        _debtContext.BillLineParticipants
            .Where(q => q.UserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.UserId, newUserId));

        // LedgerRecords: records between old and new would become a debt to oneself -> deleted

        _debtContext.LedgerRecords
            .Where(q => (q.CreditorUserId == oldUserId && q.DebtorUserId == newUserId)
                || (q.CreditorUserId == newUserId && q.DebtorUserId == oldUserId))
            .ExecuteDelete();

        // LedgerRecords where oldUser is creditor; the same counterpart in the same bill -> amounts added

        var creditorClashes = _debtContext.LedgerRecords
            .Where(q => q.CreditorUserId == oldUserId
                && _debtContext.LedgerRecords.Any(n => n.CreditorUserId == newUserId && n.DebtorUserId == q.DebtorUserId && n.BillId == q.BillId))
            .ToList();
        foreach (var clash in creditorClashes)
        {
            var kept = _debtContext.LedgerRecords.First(n => n.CreditorUserId == newUserId && n.DebtorUserId == clash.DebtorUserId && n.BillId == clash.BillId);
            kept.Amount += clash.Amount;
            _debtContext.LedgerRecords.Remove(clash);
        }
        _debtContext.SaveChanges();

        _debtContext.LedgerRecords
            .Where(q => q.CreditorUserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.CreditorUserId, newUserId));

        // LedgerRecords where oldUser is debtor

        var debtorClashes = _debtContext.LedgerRecords
            .Where(q => q.DebtorUserId == oldUserId
                && _debtContext.LedgerRecords.Any(n => n.DebtorUserId == newUserId && n.CreditorUserId == q.CreditorUserId && n.BillId == q.BillId))
            .ToList();
        foreach (var clash in debtorClashes)
        {
            var kept = _debtContext.LedgerRecords.First(n => n.DebtorUserId == newUserId && n.CreditorUserId == clash.CreditorUserId && n.BillId == clash.BillId);
            kept.Amount += clash.Amount;
            _debtContext.LedgerRecords.Remove(clash);
        }
        _debtContext.SaveChanges();

        _debtContext.LedgerRecords
            .Where(q => q.DebtorUserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.DebtorUserId, newUserId));

        // User contacts: old and new as each other's contact -> deleted; duplicates -> the kept user's link stays

        _debtContext.UserContactsLinks
            .Where(t => (t.UserId == oldUserId && t.ContactUserId == newUserId)
                || (t.UserId == newUserId && t.ContactUserId == oldUserId))
            .ExecuteDelete();

        _debtContext.UserContactsLinks
            .Where(t => t.UserId == oldUserId
                && _debtContext.UserContactsLinks.Any(c => c.UserId == newUserId && c.ContactUserId == t.ContactUserId))
            .ExecuteDelete();

        _debtContext.UserContactsLinks
            .Where(t => t.UserId == oldUserId)
            .ExecuteUpdate(t => t.SetProperty(t => t.UserId, newUserId));

        _debtContext.UserContactsLinks
            .Where(t => t.ContactUserId == oldUserId
                && _debtContext.UserContactsLinks.Any(c => c.ContactUserId == newUserId && c.UserId == t.UserId))
            .ExecuteDelete();

        _debtContext.UserContactsLinks
            .Where(t => t.ContactUserId == oldUserId)
            .ExecuteUpdate(t => t.SetProperty(t => t.ContactUserId, newUserId));

        // Spendings (budgets): both in the same bill -> one spending with both shares

        var spendingClashes = _debtContext.Spendings
            .Where(q => q.UserId == oldUserId
                && _debtContext.Spendings.Any(n => n.UserId == newUserId && n.BillId == q.BillId))
            .ToList();
        foreach (var clash in spendingClashes)
        {
            var kept = _debtContext.Spendings.First(n => n.UserId == newUserId && n.BillId == clash.BillId);
            kept.Amount += clash.Amount;
            kept.PaymentAmount += clash.PaymentAmount;
            kept.Portion += clash.Portion;
            _debtContext.Spendings.Remove(clash);
        }
        _debtContext.SaveChanges();

        _debtContext.Spendings
            .Where(q => q.UserId == oldUserId)
            .ExecuteUpdate(s => s.SetProperty(p => p.UserId, newUserId));

        // BillParticipants: duplicates dropped

        _debtContext.BillParticipants
            .Where(q => q.UserId == oldUserId
                && _debtContext.BillParticipants.Any(n => n.UserId == newUserId && n.BillId == q.BillId))
            .ExecuteDelete();

        _debtContext.BillParticipants
            .Where(q => q.UserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.UserId, newUserId));

        // NotificationSubscriptions both ways: between old and new -> deleted; duplicates -> the kept row stays,
        // confirmed if either was

        _debtContext.NotificationSubscriptions
            .Where(q => (q.UserId == oldUserId && q.SubscriberId == newUserId)
                || (q.UserId == newUserId && q.SubscriberId == oldUserId))
            .ExecuteDelete();

        var subscriptionClashes = _debtContext.NotificationSubscriptions
            .Where(q => (q.UserId == oldUserId
                    && _debtContext.NotificationSubscriptions.Any(c => c.UserId == newUserId && c.SubscriberId == q.SubscriberId))
                || (q.SubscriberId == oldUserId
                    && _debtContext.NotificationSubscriptions.Any(c => c.SubscriberId == newUserId && c.UserId == q.UserId)))
            .ToList();
        foreach (var clash in subscriptionClashes)
        {
            var kept = clash.UserId == oldUserId
                ? _debtContext.NotificationSubscriptions.First(c => c.UserId == newUserId && c.SubscriberId == clash.SubscriberId)
                : _debtContext.NotificationSubscriptions.First(c => c.SubscriberId == newUserId && c.UserId == clash.UserId);
            kept.IsConfirmed |= clash.IsConfirmed;
            _debtContext.NotificationSubscriptions.Remove(clash);
        }
        _debtContext.SaveChanges();

        _debtContext.NotificationSubscriptions
            .Where(q => q.UserId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.UserId, newUserId));

        _debtContext.NotificationSubscriptions
            .Where(q => q.SubscriberId == oldUserId)
            .ExecuteUpdate(u => u.SetProperty(u => u.SubscriberId, newUserId));

        // Web sessions of the old user end

        _debtContext.RefreshTokens
            .Where(t => t.UserId == oldUserId)
            .ExecuteDelete();

        // API tokens of the old user are revoked, like its sessions: they go with the old user (not moved to the kept one)

        _debtContext.ApiTokens
            .Where(t => t.UserId == oldUserId)
            .ExecuteDelete();

        // Profile

        newUser.DisplayName = newUser.DisplayName ?? oldUser.DisplayName;
        newUser.TelegramId = newUser.TelegramId ?? oldUser.TelegramId;
        newUser.TelegramUserName = newUser.TelegramUserName ?? oldUser.TelegramUserName;
        newUser.Phone = newUser.Phone ?? oldUser.Phone;
        newUser.Email = newUser.Email ?? oldUser.Email;
        newUser.TelegramBotEnabled = newUser.TelegramBotEnabled || oldUser.TelegramBotEnabled;
        newUser.Role = oldUser.Role == UserRole.Admin ? UserRole.Admin : newUser.Role;

        // Web sign-in: the kept user's login and password win; without either, it takes the old user's.
        // The old row gives up its login first so the unique login index holds at every statement.
        string? movedLogin = null;
        string? movedPasswordHash = null;
        if (newUser.Login is null && newUser.PasswordHash is null)
        {
            movedLogin = oldUser.Login;
            movedPasswordHash = oldUser.PasswordHash;
        }
        oldUser.Login = null;
        oldUser.PasswordHash = null;
        _debtContext.SaveChanges();

        if (movedLogin is not null || movedPasswordHash is not null)
        {
            newUser.Login = movedLogin;
            newUser.PasswordHash = movedPasswordHash;
        }

        _debtContext.Users.Remove(oldUser);

        _debtContext.SaveChanges();

        transaction.Commit();

        return true;
    }

    public ProfileModel? GetProfile(Guid id)
    {
        return _debtContext.Users
            .Where(u => u.Id == id)
            .Select(u => new ProfileModel
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Phone = u.Phone,
                Email = u.Email,
                TelegramId = u.TelegramId,
                TelegramUserName = u.TelegramUserName,
                TelegramBotEnabled = u.TelegramBotEnabled,
                Role = u.Role,
                Login = u.Login,
                HasPassword = u.PasswordHash != null
            })
            .FirstOrDefault();
    }

    public bool UpdateProfile(Guid id, string displayName, string? phone, string? email)
    {
        var user = _debtContext.Users.FirstOrDefault(u => u.Id == id);
        if (user is null)
        {
            return false;
        }

        user.DisplayName = displayName;
        user.Phone = phone;
        user.Email = email;
        _debtContext.SaveChanges();

        return true;
    }

    private IQueryable<AdminUserModel> adminUsers(IQueryable<User> users)
    {
        var now = DateTime.UtcNow;
        return users.Select(u => new AdminUserModel
        {
            Id = u.Id,
            DisplayName = u.DisplayName,
            TelegramId = u.TelegramId,
            TelegramUserName = u.TelegramUserName,
            Phone = u.Phone,
            Email = u.Email,
            Role = u.Role,
            TelegramBotEnabled = u.TelegramBotEnabled,
            Login = u.Login,
            HasPassword = u.PasswordHash != null,
            BillCount = _debtContext.BillParticipants.Count(p => p.UserId == u.Id),
            ContactCount = _debtContext.UserContactsLinks.Count(c => c.UserId == u.Id),
            ApiTokenCount = _debtContext.ApiTokens.Count(t => t.UserId == u.Id && t.RevokedAt == null && (t.ExpiresAt == null || t.ExpiresAt > now)),
            FirstBillDate = _debtContext.BillParticipants
                .Where(p => p.UserId == u.Id)
                .Min(p => (DateTime?)p.Bill.Date)
        });
    }

    public PagingResult<AdminUserModel> GetAdminUsers(string? search, UserRole? role, int pageNumber = 0, int? countPerPage = null)
    {
        var users = _debtContext.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            users = users.Where(u => u.DisplayName.ToLower().Contains(term)
                || (u.TelegramUserName ?? "").ToLower().Contains(term)
                || (u.Login ?? "").ToLower().Contains(term));
        }

        if (role is not null)
        {
            users = users.Where(u => u.Role == role);
        }

        return adminUsers(users.OrderBy(u => u.DisplayName).ThenBy(u => u.Id))
            .ToPagingResult(pageNumber, countPerPage);
    }

    public AdminUserModel? GetAdminUser(Guid id)
    {
        return adminUsers(_debtContext.Users.Where(u => u.Id == id)).FirstOrDefault();
    }

    public List<AdminUserModel> GetAdminUsersWithTelegram()
    {
        return adminUsers(_debtContext.Users.Where(u => u.TelegramId != null || u.TelegramUserName != null)).ToList();
    }

    // Text fields match exactly, ignoring case. (ILike treated "_" and "%" in usernames as wildcards.)
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
            user = _debtContext.Users.FirstOrDefault(u => u.TelegramUserName!.ToLower() == model.TelegramUserName.ToLower());
        }
        if (user is null && !string.IsNullOrEmpty(model.Phone))
        {
            user = _debtContext.Users.FirstOrDefault(u => u.Phone!.ToLower() == model.Phone.ToLower());
        }
        if (user is null && !string.IsNullOrEmpty(model.Email))
        {
            user = _debtContext.Users.FirstOrDefault(u => u.Email!.ToLower() == model.Email.ToLower());
        }
        if (user is null && !string.IsNullOrEmpty(model.QueryString))
        {
            var queryString = model.QueryString.ToLower();
            user = _debtContext.Users.FirstOrDefault(u => u.TelegramId.ToString() == model.QueryString);
            user ??= _debtContext.Users.FirstOrDefault(u => (u.TelegramUserName ?? "").ToLower() == queryString);
            user ??= _debtContext.Users.FirstOrDefault(u => (u.Phone ?? "").ToLower() == queryString);
            user ??= _debtContext.Users.FirstOrDefault(u => (u.Email ?? "").ToLower() == queryString);
        }

        var searchString = model.DisplayName ?? model.QueryString;
        if (user is null
            && userId is not null
            && !string.IsNullOrEmpty(searchString))
        {
            user = _debtContext.UserContactsLinks
                .Where(t => t.UserId == userId && (t.DisplayName ?? "").ToLower() == searchString.ToLower())
                .Select(t => t.ContactUser)
                .FirstOrDefault();
        }

        if (user is null)
            return null;

        return _mapper.Map<UserModel>(user);
    }

    public bool SetBotActiveState(Guid userId, bool stateToSet)
    {
        var user = _debtContext
            .Users
            .FirstOrDefault(u => u.Id == userId);

        if (user == null)
            return false;

        user.TelegramBotEnabled = stateToSet;

        _debtContext.SaveChanges();

        return true;
    }
}