using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Services;

public class DebtService : IDebtService
{
    private readonly DebtContext _debtContext;
    private readonly IMapper _mapper;

    public DebtService(DebtContext debtContext, IMapper mapper)
    {
        this._debtContext = debtContext;
        this._mapper = mapper;
    }

    public List<DebtModel> GetAll()
    {
        return _debtContext
            .DebtsTable
            .ProjectTo<DebtModel>(_mapper.ConfigurationProvider)
            .ToList();
    }

    public List<DebtModel> Get(Guid id)
    {
        return _debtContext
            .DebtsTable
            .Where(t => t.CreditorUserId == id)
            .ProjectTo<DebtModel>(_mapper.ConfigurationProvider)
            .ToList();
    }
}
