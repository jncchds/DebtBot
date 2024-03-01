using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.Extensions;
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
            .Debts
            .ProjectTo<DebtModel>(_mapper.ConfigurationProvider)
            .ToList();
    }

    public PagingResult<DebtModel> GetForUser(Guid userId, int pageNumber = 0, int? countPerPage = null)
    {
        return _debtContext
            .Debts
            .Where(t => t.CreditorUserId == userId)
            .Include(t => t.CreditorUser)
            .Include(t => t.DebtorUser)
            .ThenInclude(t => t.ContactUser)
            .ProjectTo<DebtModel>(_mapper.ConfigurationProvider)
            .ToPagingResult(pageNumber, countPerPage);
    }
}
