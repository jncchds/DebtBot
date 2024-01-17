using AutoMapper;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models;

namespace DebtBot.Services
{
    public class UserService : IUserService
    {
        private readonly DebtContext _db;
        private readonly IMapper _mapper;

        public UserService(DebtContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public IEnumerable<UserModel> GetUsers()
        {
            var users = _db.Users.ToList();
            return _mapper.Map<IEnumerable<UserModel>>(users);
        }

        public UserModel? GetUserById(Guid id)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == id);
            return _mapper.Map<UserModel>(user);
        }

        public void AddUser(UserModel user)
        {
            var entity = _mapper.Map<User>(user);
            _db.Users.Add(entity);
            _db.SaveChanges();
        }

        public void UpdateUser(UserModel user)
        {
            var entity = _mapper.Map<User>(user);
            _db.Users.Update(entity);
            _db.SaveChanges();
        }

        public void DeleteUser(Guid id)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                _db.Users.Remove(user);
                _db.SaveChanges();
            }
        }
    }
}
