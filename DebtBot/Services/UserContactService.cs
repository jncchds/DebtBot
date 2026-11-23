using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Contact;
using DebtBot.Models.User;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Services;

public class UserContactService : IUserContactService
{
    private readonly DebtContext _debtContext;
    private readonly IMapper _mapper;

    public UserContactService(DebtContext debtContext, IMapper mapper)
    {
        _debtContext = debtContext;
        _mapper = mapper;
    }

    public IEnumerable<UserModel> Get()
    {
        var contactLinks = _debtContext
            .UserContactsLinks
            .Include(t => t.ContactUser)
            .ProjectTo<UserModel>(_mapper.ConfigurationProvider)
            .ToList();

        return contactLinks;
    }

    public IEnumerable<UserModel> Get(Guid id)
    {
        var contacts = _debtContext.Users
            .Include(t => t.UserContacts)
            .ThenInclude(t => t.ContactUser)
            .Where(t => t.Id == id)
            .SelectMany(t => t.UserContacts)
            .ProjectTo<UserModel>(_mapper.ConfigurationProvider)
            .ToList();

        return contacts;
    }

    public void AddContact(Guid id, UserModel contact)
    {
        var user = _debtContext.Users.FirstOrDefault(t => t.Id == id);
        if (user == null)
        {
            return;
        }

        var contactModel = _mapper.Map<User>(contact);

        var contactUser = FindUser(contactModel);
        if (contactUser == null)
        {
            _debtContext.Users.Add(contactModel);
            contactUser = contactModel;
        }

        var link = new UserContactLink()
        {
            DisplayName = contact.DisplayName,
            User = user,
            ContactUser = contactUser
        };

        _debtContext.UserContactsLinks.Add(link);

        _debtContext.SaveChanges();
    }

    public void RequestSubscription(Guid userId, Guid contactId, bool forceSubscribe = false)
    {
        var subscription = _debtContext.NotificationSubscriptions.FirstOrDefault(t => t.UserId == contactId && t.SubscriberId == userId);
        if (subscription == null)
        {
            subscription = new NotificationSubscription()
            {
                UserId = contactId,
                SubscriberId = userId,
                IsConfirmed = forceSubscribe
            };

            _debtContext.NotificationSubscriptions.Add(subscription);

            _debtContext.SaveChanges();
        }
    }

    public void ConfirmSubscription(Guid userId, Guid contactId)
    {
        var subscription = _debtContext.NotificationSubscriptions.FirstOrDefault(t => t.UserId == userId && t.SubscriberId == contactId);
        if (subscription != null)
        {
            subscription.IsConfirmed = true;

            _debtContext.SaveChanges();
        }
    }

    public void DeclineSubscription(Guid userId, Guid contactId)
    {
        var subscription = _debtContext.NotificationSubscriptions.FirstOrDefault(t => t.UserId == userId && t.SubscriberId == contactId);
        if (subscription != null)
        {
            _debtContext.NotificationSubscriptions.Remove(subscription);

            _debtContext.SaveChanges();
        }
    }

    public List<ContactModel> GetContacts(Guid userId)
    {
        return getContacts(userId, null);
    }

    public ContactModel? GetContact(Guid userId, Guid contactUserId)
    {
        return getContacts(userId, contactUserId).FirstOrDefault();
    }

    private List<ContactModel> getContacts(Guid userId, Guid? contactUserId)
    {
        var links = _debtContext.UserContactsLinks
            .Where(l => l.UserId == userId && (contactUserId == null || l.ContactUserId == contactUserId))
            .Select(l => new ContactModel
            {
                Id = l.ContactUserId,
                DisplayName = l.DisplayName,
                UserDisplayName = l.ContactUser.DisplayName,
                TelegramId = l.ContactUser.TelegramId,
                TelegramUserName = l.ContactUser.TelegramUserName,
                TelegramBotEnabled = l.ContactUser.TelegramBotEnabled
            })
            .ToList();

        // I'm the subscriber: notifications about the contact's bills
        var mine = _debtContext.NotificationSubscriptions
            .Where(s => s.SubscriberId == userId)
            .ToDictionary(s => s.UserId, s => s.IsConfirmed);
        // They're the subscriber: notifications about my bills
        var theirs = _debtContext.NotificationSubscriptions
            .Where(s => s.UserId == userId)
            .ToDictionary(s => s.SubscriberId, s => s.IsConfirmed);

        foreach (var contact in links)
        {
            contact.MySubscription = toState(mine, contact.Id);
            contact.TheirSubscription = toState(theirs, contact.Id);
        }

        return links
            .OrderBy(c => c.DisplayName ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id)
            .ToList();
    }

    private static SubscriptionState toState(Dictionary<Guid, bool> subscriptions, Guid contactUserId) =>
        subscriptions.TryGetValue(contactUserId, out var confirmed)
            ? (confirmed ? SubscriptionState.Confirmed : SubscriptionState.Pending)
            : SubscriptionState.None;

    public void EnsureContact(Guid userId, Guid contactUserId, string displayName)
    {
        var contact = _debtContext.UserContactsLinks.FirstOrDefault(t => t.UserId == userId && t.ContactUserId == contactUserId);
        if (contact is null)
        {
            contact = new UserContactLink()
            {
                UserId = userId,
                ContactUserId = contactUserId,
                DisplayName = displayName
            };
            _debtContext.UserContactsLinks.Add(contact);
            _debtContext.SaveChanges();
        }
    }

    public bool RenameContact(Guid userId, Guid contactUserId, string displayName)
    {
        var contact = _debtContext.UserContactsLinks.FirstOrDefault(t => t.UserId == userId && t.ContactUserId == contactUserId);
        if (contact is null)
        {
            return false;
        }

        contact.DisplayName = displayName;
        _debtContext.SaveChanges();
        return true;
    }

    public bool RemoveContact(Guid userId, Guid contactUserId)
    {
        var contact = _debtContext.UserContactsLinks.FirstOrDefault(t => t.UserId == userId && t.ContactUserId == contactUserId);
        if (contact is null)
        {
            return false;
        }

        _debtContext.UserContactsLinks.Remove(contact);
        _debtContext.SaveChanges();
        return true;
    }

    private User? FindUser(User user)
    {
        return _debtContext.Users.FirstOrDefault(t =>
            (t.Id == user.Id) ||
            (t.TelegramId ?? -1) == user.TelegramId ||
            (t.TelegramUserName ?? "N/A") == user.TelegramUserName ||
            (t.Phone ?? "N/A") == user.Phone ||
            (t.Email ?? "N/A") == user.Email);
    }
}