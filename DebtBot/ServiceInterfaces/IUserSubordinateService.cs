using DebtBot.Models;

namespace DebtBot.ServiceInterfaces;

public interface IUserSubordinateService
{
    void AddSubordinate(Guid id, UserModel subordinate);
    IEnumerable<UserModel> Get();
    IEnumerable<UserModel> Get(Guid id);
}