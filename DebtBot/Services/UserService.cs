using DebtBot.DB;
using DebtBot.DB.Entities;

namespace DebtBot.Services
{
    public class UserService : IUserService
    {
        private readonly DebtContext _db;

        public UserService(DebtContext db)
        {
            _db = db;
        }
        public IEnumerable<User> GetUsers()
        {
            return _db.Users.ToList();
        }

        public User? GetUserById(Guid id)
        {
            return _db.Users.FirstOrDefault(u => u.Id == id);
        }

        public void AddUser(User user)
        {
            _db.Users.Add(user);
            _db.SaveChanges();
        }

        public void UpdateUser(User user)
        {
            _db.Users.Update(user);
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
