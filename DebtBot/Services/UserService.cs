using AutoMapper;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models;
using DebtBot.ServiceInterfaces;

namespace DebtBot.Services
{
    public class UserService : IUserService
    {
        private readonly DebtContext debtContext;
        private readonly IMapper mapper;

        public UserService(DebtContext db, IMapper mapper)
        {
            this.debtContext = db;
            this.mapper = mapper;
        }

        public IEnumerable<UserModel> GetUsers()
        {
            var users = debtContext.Users.ToList();
            return mapper.Map<IEnumerable<UserModel>>(users);
        }

        public UserModel? GetUserById(Guid id)
        {
            var user = debtContext.Users.FirstOrDefault(u => u.Id == id);
            return mapper.Map<UserModel>(user);
        }

        public void AddUser(UserModel user)
        {
            var entity = mapper.Map<User>(user);
            debtContext.Users.Add(entity);
            debtContext.SaveChanges();
        }

        public void UpdateUser(UserModel user)
        {
            var entity = mapper.Map<User>(user);
            debtContext.Users.Update(entity);
            debtContext.SaveChanges();
        }

        public void DeleteUser(Guid id)
        {
            var user = debtContext.Users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                debtContext.Users.Remove(user);
                debtContext.SaveChanges();
            }
        }
    }
}
