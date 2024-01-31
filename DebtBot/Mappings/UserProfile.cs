using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models.User;

namespace DebtBot.Mapping;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserModel>().ReverseMap();

        CreateMap<UserCreationModel, User>();
    }
}