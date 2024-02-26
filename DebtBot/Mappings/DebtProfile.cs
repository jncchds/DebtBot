using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models;

namespace DebtBot.Mappings;

public class DebtProfile : Profile
{
    public DebtProfile()
    {
        CreateProjection<Debt, DebtModel>();
    }
}
