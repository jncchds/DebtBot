using DebtBot.Models;

namespace DebtBot.Interfaces.Services;

public interface IUserContactService
{
    void AddContact(Guid id, UserModel contact);
    IEnumerable<UserModel> Get();
    IEnumerable<UserModel> Get(Guid id);
}