using DebtBot.Models.Contact;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IUserContactService
{
    void AddContact(Guid id, UserModel contact);
    IEnumerable<UserModel> Get();
    IEnumerable<UserModel> Get(Guid id);
    void RequestSubscription(Guid userId, Guid contactId, bool forceSubscribe = false);
    /// <summary>Confirms <paramref name="contactId"/>'s request to follow <paramref name="userId"/>'s bills; false when there is none.</summary>
    bool ConfirmSubscription(Guid userId, Guid contactId);
    /// <summary>Removes <paramref name="contactId"/>'s request or subscription to <paramref name="userId"/>; false when there is none.</summary>
    bool DeclineSubscription(Guid userId, Guid contactId);
    /// <summary>The user's contacts with subscription states, ordered by name.</summary>
    List<ContactModel> GetContacts(Guid userId);
    ContactModel? GetContact(Guid userId, Guid contactUserId);
    /// <summary>Adds the contact link if it's missing; an existing link keeps its name.</summary>
    void EnsureContact(Guid userId, Guid contactUserId, string displayName);
    bool RenameContact(Guid userId, Guid contactUserId, string displayName);
    bool RemoveContact(Guid userId, Guid contactUserId);
}