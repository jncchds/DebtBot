using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models.User;

namespace DebtBot.Mappings;

public class UserContactProfile : Profile
{
    public UserContactProfile()
    {
        CreateProjection<UserContactLink, UserModel>()
            .ForMember(um => um.Id, opt => opt.MapFrom(us => us.ContactUser.Id))
            .ForMember(um => um.TelegramId, opt => opt.MapFrom(us => us.ContactUser.TelegramId))
            .ForMember(um => um.TelegramUserName, opt => opt.MapFrom(us => us.ContactUser.TelegramUserName))
            .ForMember(um => um.Phone, opt => opt.MapFrom(us => us.ContactUser.Phone))
            .ForMember(um => um.Email, opt => opt.MapFrom(us => us.ContactUser.Email))
            .ForMember(um => um.DisplayName, opt => opt.MapFrom(us => us.DisplayName));

        CreateMap<UserContactLink, UserModel>()
            .ForMember(um => um.Id, opt => opt.MapFrom(us => us.ContactUser.Id))
            .ForMember(um => um.TelegramId, opt => opt.MapFrom(us => us.ContactUser.TelegramId))
            .ForMember(um => um.TelegramUserName, opt => opt.MapFrom(us => us.ContactUser.TelegramUserName))
            .ForMember(um => um.Phone, opt => opt.MapFrom(us => us.ContactUser.Phone))
            .ForMember(um => um.Email, opt => opt.MapFrom(us => us.ContactUser.Email))
            .ForMember(um => um.DisplayName, opt => opt.MapFrom(us => us.DisplayName));

        CreateProjection<UserContactLink, UserDisplayModel>()
            .ForMember(um => um.Id, opt => opt.MapFrom(us => us.ContactUser.Id))
            .ForMember(um => um.TelegramId, opt => opt.MapFrom(us => us.ContactUser.TelegramId))
            .ForMember(um => um.TelegramUserName, opt => opt.MapFrom(us => us.ContactUser.TelegramUserName))
            .ForMember(um => um.DisplayName, opt => opt.MapFrom(us => us.DisplayName));

        CreateMap<UserContactLink, UserDisplayModel>()
            .ForMember(um => um.Id, opt => opt.MapFrom(us => us.ContactUser.Id))
            .ForMember(um => um.TelegramId, opt => opt.MapFrom(us => us.ContactUser.TelegramId))
            .ForMember(um => um.TelegramUserName, opt => opt.MapFrom(us => us.ContactUser.TelegramUserName))
            .ForMember(um => um.DisplayName, opt => opt.MapFrom(us => us.DisplayName));
    }
}