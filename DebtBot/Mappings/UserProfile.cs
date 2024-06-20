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

        CreateMap<User, UserDisplayModel>();

        CreateMap<UserSearchModel, UserCreationModel>()
            .ForMember(q => q.DisplayName, opt => opt.MapFrom(q => q.DisplayName ?? q.QueryString));

        CreateMap<UserSearchModel, User>()
            .ForMember(q => q.DisplayName, opt => opt.MapFrom(q => q.DisplayName??q.QueryString));
    }
}