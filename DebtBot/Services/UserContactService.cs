using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models;
using DebtBot.ServiceInterfaces;
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

    private User? FindUser(User user)
    {
        return _debtContext.Users.FirstOrDefault(t =>
            (t.Id == user.Id) ||
            (t.TelegramId ?? -1) == user.TelegramId ||
            (t.Phone ?? "N/A") == user.Phone ||
            (t.Email ?? "N/A") == user.Email);
    }
}