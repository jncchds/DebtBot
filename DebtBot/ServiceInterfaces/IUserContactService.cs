using DebtBot.Models;

namespace DebtBot.ServiceInterfaces;

public interface IUserContactService
{
    void AddContact(Guid id, UserModel contact);
    IEnumerable<UserModel> Get();
    IEnumerable<UserModel> Get(Guid id);
}