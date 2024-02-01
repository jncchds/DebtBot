using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.DB.Enums;
using DebtBot.Interfaces.Services;
using DebtBot.Models.User;

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
}