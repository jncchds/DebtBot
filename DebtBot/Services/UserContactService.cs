using AutoMapper;
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
        var contactLinks = _debtContext.UserContactsLinks.Include(t => t.ContactUser).ToList();
        var res = _mapper.Map<IEnumerable<UserModel>>(contactLinks);
        return res;
    }

    public IEnumerable<UserModel> Get(Guid id)
    {
        var user = _debtContext.Users
            .Include(t => t.UserContacts)
            .ThenInclude(t => t.ContactUser)
            .FirstOrDefault(t => t.Id == id);
        if (user == null)
        {
            return null;
        }

        var contacts = user.UserContacts;
        return _mapper.Map<IEnumerable<UserModel>>(contacts);
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