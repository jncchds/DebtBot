using DebtBot.Models;

namespace DebtBot.ServiceInterfaces
{
    public interface IUserService
    {
        void AddUser(UserModel user);
        void DeleteUser(Guid id);
        UserModel? GetUserById(Guid id);
        IEnumerable<UserModel> GetUsers();
        void UpdateUser(UserModel user);
    }
}