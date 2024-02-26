using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models;

namespace DebtBot.Mappings;

public class SpendingProfile : Profile
{
    public SpendingProfile()
    {
        CreateMap<Spending, SpendingModel>()
            .IgnoreAllPropertiesWithAnInaccessibleSetter();
    }
}
