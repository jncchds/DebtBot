using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IUserContactService
{
    void AddContact(Guid id, UserModel contact);
    IEnumerable<UserModel> Get();
    IEnumerable<UserModel> Get(Guid id);
    void RequestSubscription(Guid userId, Guid contactId, bool forceSubscribe = false);
    void ConfirmSubscription(Guid userId, Guid contactId);
    void DeclineSubscription(Guid userId, Guid contactId);
}