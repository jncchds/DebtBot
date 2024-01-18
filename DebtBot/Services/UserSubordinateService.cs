using AutoMapper;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models;
using DebtBot.ServiceInterfaces;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Services;

public class UserSubordinateService : IUserSubordinateService
{
    private readonly DebtContext _debtContext;
    private readonly IMapper _mapper;

    public UserSubordinateService(DebtContext debtContext, IMapper mapper)
    {
        _debtContext = debtContext;
        _mapper = mapper;
    }

    public IEnumerable<UserModel> Get()
    {
        var subordinates = _debtContext.UserSubordinates.Include(t => t.SubordinateUser).ToList();
        var res = _mapper.Map<IEnumerable<UserModel>>(subordinates);
        return res;
    }

    public IEnumerable<UserModel> Get(Guid id)
    {
        var user = _debtContext.Users
            .Include(t => t.UserSubordinates)
            .ThenInclude(t => t.SubordinateUser)
            .FirstOrDefault(t => t.Id == id);
        if (user == null)
        {
            return null;
        }

        var subordinates = user.UserSubordinates;
        return _mapper.Map<IEnumerable<UserModel>>(subordinates);
    }

    public void AddSubordinate(Guid id, UserModel subordinate)
    {
        var user = _debtContext.Users.FirstOrDefault(t => t.Id == id);
        if (user == null)
        {
            return;
        }

        var subordinateModel = _mapper.Map<User>(subordinate);

        var subordinateUser = FindUser(subordinateModel);
        if (subordinateUser == null)
        {
            _debtContext.Users.Add(subordinateModel);
            subordinateUser = subordinateModel;
        }

        var link = new UserSubordinate()
        {
            DisplayName = subordinate.DisplayName,
            User = user,
            SubordinateUser = subordinateUser
        };

        _debtContext.UserSubordinates.Add(link);

        _debtContext.SaveChanges();
    }

    private User? FindUser(User user)
    {
        return _debtContext.Users.FirstOrDefault(t =>
            (t.Id == user.Id) ||
            (t.TelegramId ?? -1) == user.TelegramId ||
            (t.Phone ?? "N/A") == user.Phone ||
            (t.Email ?? "N/A") == user.Email);
    }
}