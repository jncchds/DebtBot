using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models;

namespace DebtBot.Mappings;

public class UserContactProfile : Profile
{
    public UserContactProfile()
    {
        CreateMap<UserContactLink, UserModel>()
            .ForMember(um => um.Id, opt => opt.MapFrom(us => us.ContactUser.Id))
            .ForMember(um => um.TelegramId, opt => opt.MapFrom(us => us.ContactUser.TelegramId))
            .ForMember(um => um.Phone, opt => opt.MapFrom(us => us.ContactUser.Phone))
            .ForMember(um => um.Email, opt => opt.MapFrom(us => us.ContactUser.Email))
            .ForMember(um => um.DisplayName, opt => opt.MapFrom(us => us.DisplayName));
    }
}