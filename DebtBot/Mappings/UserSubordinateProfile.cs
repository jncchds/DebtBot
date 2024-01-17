using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models;

namespace DebtBot.Mappings
{
    public class UserSubordinateProfile : Profile
    {
        public UserSubordinateProfile()
        {
            CreateMap<UserSubordinate, UserModel>()
                .ForMember(um => um.Id, opt => opt.MapFrom(us => us.SubordinateUser.Id))
                .ForMember(um => um.TelegramId, opt => opt.MapFrom(us => us.SubordinateUser.TelegramId))
                .ForMember(um => um.Phone, opt => opt.MapFrom(us => us.SubordinateUser.Phone))
                .ForMember(um => um.Email, opt => opt.MapFrom(us => us.SubordinateUser.Email))
                .ForMember(um => um.DisplayName, opt => opt.MapFrom(us => us.DisplayName));
        }
    }
}
