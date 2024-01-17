using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models;

namespace DebtBot.Mapping
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, UserModel>().ReverseMap();
        }
    }
}
