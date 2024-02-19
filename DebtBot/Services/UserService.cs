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

    public void AddUser(UserCreationModel user)
    {
        var entity = _mapper.Map<User>(user);
        _debtContext.Users.Add(entity);
        _debtContext.SaveChanges();
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

        // Updates/Deletes Debts where oldUser is creditor

        _debtContext.Debts
            .Where(t => 
                t.CreditorUserId == oldUserId 
                && !_debtContext.Debts
                    .Any(k => 
                        k.DebtorUserId == t.DebtorUserId
                        && k.CurrencyCode == t.CurrencyCode
                        && k.CreditorUserId == newUserId))
            .ExecuteUpdate(t => t.SetProperty(t => t.CreditorUserId, newUserId));

        _debtContext.Debts
            .Where(t => t.CreditorUserId == newUserId)
            .ExecuteUpdate(t => t.SetProperty(k => k.Amount, k => k.Amount + _debtContext.Debts
                .Where(q => q.DebtorUserId == k.DebtorUserId && q.CurrencyCode == k.CurrencyCode && q.CreditorUserId == oldUserId)
                .Sum(q => q.Amount)));

        _debtContext.Debts
            .Where(t => t.CreditorUserId == oldUserId)
            .ExecuteDelete();

        // Updates/Deletes debts where oldUser is debtor

        _debtContext.Debts
            .Where(t =>
                t.DebtorUserId == oldUserId
                && !_debtContext.Debts
                    .Any(k =>
                        k.CreditorUserId == t.CreditorUserId
                        && k.CurrencyCode == t.CurrencyCode
                        && k.DebtorUserId == newUserId))
            .ExecuteUpdate(t => t.SetProperty(t => t.DebtorUserId, newUserId));

        _debtContext.Debts
            .Where(t => t.DebtorUserId == newUserId)
            .ExecuteUpdate(t => t.SetProperty(k => k.Amount, k => k.Amount + _debtContext.Debts
                .Where(q => q.CreditorUserId == k.CreditorUserId && q.CurrencyCode == k.CurrencyCode && q.DebtorUserId == oldUserId)
                .Sum(q => q.Amount)));

        _debtContext.Debts
            .Where(t => t.DebtorUserId == oldUserId)
            .ExecuteDelete();

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
}