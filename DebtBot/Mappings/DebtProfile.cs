using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models;

namespace DebtBot.Mappings;

public class DebtProfile : Profile
{
    public DebtProfile()
    {
        CreateProjection<Debt, DebtModel>()
            .ForMember(model => model.DebtorDisplayName, opts => opts.MapFrom(debt => debt.DebtorUser.DisplayName))
            .ForMember(model => model.CreditorDisplayName, opts => opts.MapFrom(debt => debt.CreditorUser.DisplayName));
    }
}
