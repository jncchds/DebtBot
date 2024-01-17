using AutoMapper;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models;
using DebtBot.ServiceInterfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Services
{
    public class UserSubordinateService : IUserSubordinateService
    {
        private readonly DebtContext debtContext;
        private readonly IMapper mapper;

        public UserSubordinateService(DebtContext debtContext, IMapper mapper)
        {
            this.debtContext = debtContext;
            this.mapper = mapper;
        }

        public IEnumerable<UserModel> Get()
        {
            var subordinates = debtContext.UserSubordinates.Include(t => t.SubordinateUser).ToList();
            var res = mapper.Map<IEnumerable<UserModel>>(subordinates);
            return res;
        }

        public IEnumerable<UserModel> Get(Guid id)
        {
            var user = debtContext.Users
                .Include(t => t.UserSubordinates)
                .ThenInclude(t => t.SubordinateUser)
                .FirstOrDefault(t => t.Id == id);
            if (user == null)
            {
                return null;
            }

            var subordinates = user.UserSubordinates;
            return mapper.Map<IEnumerable<UserModel>>(subordinates);
        }

        public void AddSubordinate(Guid id, UserModel subordinate)
        {
            var user = debtContext.Users.FirstOrDefault(t => t.Id == id);
            if (user == null)
            {
                return;
            }

            var subordinateModel = mapper.Map<User>(subordinate);

            var subordinateUser = FindUser(subordinateModel);
            if (subordinateUser == null)
            {
                debtContext.Users.Add(subordinateModel);
                subordinateUser = subordinateModel;
            }

            var link = new UserSubordinate()
            {
                DisplayName = subordinate.DisplayName,
                User = user,
                SubordinateUser = subordinateUser
            };

            debtContext.UserSubordinates.Add(link);

            debtContext.SaveChanges();
        }

        private User? FindUser(User user)
        {
            return debtContext.Users.FirstOrDefault(t =>
                (t.Id == user.Id) ||
                (t.TelegramId ?? -1) == user.TelegramId ||
                (t.Phone ?? "N/A") == user.Phone ||
                (t.Email ?? "N/A") == user.Email);
        }
    }
}
