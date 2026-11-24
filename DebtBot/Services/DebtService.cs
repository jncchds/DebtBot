using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.Debt;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using System.Linq.Expressions;
using Debt = DebtBot.DB.Entities.Debt;
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

    public PagingResult<DebtModel> GetAll(int pageNumber = 0, int? countPerPage = null)
    {
        return _debtContext
            .Debts
            .ProjectTo<DebtModel>(_mapper.ConfigurationProvider)
            .ToPagingResult(pageNumber, countPerPage);
    }

    public PagingResult<DebtModel> GetForUser(Guid userId, int pageNumber = 0, int? countPerPage = null)
    {
        return _debtContext
            .Debts
            .Where(t => t.CreditorUserId == userId 
                || _debtContext
                    .NotificationSubscriptions
                    .Where(s => s.IsConfirmed)
                    .Any(s => s.UserId == t.CreditorUserId && s.SubscriberId == userId))
            .Include(t => t.CreditorUser)
            .Include(t => t.DebtorUser)
            .ThenInclude(t => t.ContactUser)
            .OrderBy(t => t.CreditorUserId)
            .ThenBy(t => (t.DebtorUser.ContactUser.TelegramUserName ?? t.DebtorUser.DisplayName).ToUpper())
            .ThenBy(t => t.CurrencyCode)
            .ProjectTo<DebtModel>(_mapper.ConfigurationProvider)
            .ToPagingResult(pageNumber, countPerPage);
    }

    public PagingResult<DebtModel> GetOwn(Guid userId, DebtListQuery filter, bool ownNames = false)
    {
        var query = _debtContext
            .Debts
            // Casts to double are for SQLite (tests), which compares and orders decimals as text; Postgres gives the same result.
            .Where(t => t.CreditorUserId == userId && (double)t.Amount != 0);

        if (filter.WithUserId != null)
        {
            query = query.Where(t => t.DebtorUserId == filter.WithUserId);
        }

        if (filter.CurrencyCode != null)
        {
            query = query.Where(t => t.CurrencyCode == filter.CurrencyCode);
        }

        query = filter.Direction switch
        {
            DebtDirection.OwedToMe => query.Where(t => (double)t.Amount > 0),
            DebtDirection.IOwe => query.Where(t => (double)t.Amount < 0),
            _ => query,
        };

        var desc = filter.SortOrder == SortOrder.Desc;
        // The person is the name as shown: the user's contact name, or for someone else's list (ownNames) the
        // person's own display name. Amount is signed: ascending puts the largest debt first.
        Expression<Func<Debt, string>> name = ownNames
            ? t => t.DebtorUser.ContactUser.DisplayName.ToUpper()
            : t => t.DebtorUser.DisplayName.ToUpper();
        var sorted = filter.SortBy switch
        {
            DebtSortField.Amount => (desc ? query.OrderByDescending(t => (double)t.Amount) : query.OrderBy(t => (double)t.Amount))
                .ThenBy(name),
            DebtSortField.Currency => (desc ? query.OrderByDescending(t => t.CurrencyCode) : query.OrderBy(t => t.CurrencyCode))
                .ThenBy(name),
            _ => (desc ? query.OrderByDescending(name) : query.OrderBy(name))
                .ThenBy(t => t.CurrencyCode),
        };

        sorted = sorted
            .ThenBy(t => t.DebtorUserId)
            .ThenBy(t => t.CurrencyCode);

        if (!ownNames)
        {
            return sorted
                .ProjectTo<DebtModel>(_mapper.ConfigurationProvider)
                .ToPagingResult(filter.PageNumber, filter.CountPerPage);
        }

        return sorted
            .Select(t => new DebtModel
            {
                CreditorUser = new UserDisplayModel
                {
                    Id = t.CreditorUser.Id,
                    DisplayName = t.CreditorUser.DisplayName,
                    TelegramId = t.CreditorUser.TelegramId,
                    TelegramUserName = t.CreditorUser.TelegramUserName,
                },
                DebtorUser = new UserDisplayModel
                {
                    Id = t.DebtorUser.ContactUser.Id,
                    DisplayName = t.DebtorUser.ContactUser.DisplayName,
                    TelegramId = t.DebtorUser.ContactUser.TelegramId,
                    TelegramUserName = t.DebtorUser.ContactUser.TelegramUserName,
                },
                Amount = t.Amount,
                CurrencyCode = t.CurrencyCode,
            })
            .ToPagingResult(filter.PageNumber, filter.CountPerPage);
    }
}
