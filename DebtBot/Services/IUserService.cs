using DebtBot.DB.Entities;

namespace DebtBot.Services
{
    public interface IUserService
    {
        void AddUser(User user);
        void DeleteUser(Guid id);
        User? GetUserById(Guid id);
        IEnumerable<User> GetUsers();
        void UpdateUser(User user);
    }
}