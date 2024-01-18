using AutoMapper;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models;
using DebtBot.ServiceInterfaces;

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
        var users = _debtContext.Users.ToList();
        return _mapper.Map<IEnumerable<UserModel>>(users);
    }

    public UserModel? GetUserById(Guid id)
    {
        var user = _debtContext.Users.FirstOrDefault(u => u.Id == id);
        return _mapper.Map<UserModel>(user);
    }

    public void AddUser(UserModel user)
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

    public void DeleteUser(Guid id)
    {
        var user = _debtContext.Users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            _debtContext.Users.Remove(user);
            _debtContext.SaveChanges();
        }
    }
}