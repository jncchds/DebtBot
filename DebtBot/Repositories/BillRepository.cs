using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Extensions;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
using DebtBot.Models.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DebtBot.Repositories;

public class BillRepository : IBillRepository
{
    private readonly DebtContext _debtContext;
    private readonly IMapper _mapper;

    public BillRepository(
        DebtContext debtContext,
        IMapper mapper)
    {
        _debtContext = debtContext;
        _mapper = mapper;
    }

    public async Task<BillModel?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var bill = await _debtContext
            .Bills
            .Where(q => q.Id == id)
            .ProjectTo<BillModel>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);

        return bill;
    }

    public PagingResult<BillListModel> GetAll(AdminBillListQuery filter)
    {
        IQueryable<Bill> query = _debtContext.Bills;

        if (filter.CreatorId != null)
        {
            var creatorId = filter.CreatorId.Value;
            query = query.Where(b => b.CreatorId == creatorId);
        }

        if (filter.WithUserId != null)
        {
            var withUserId = filter.WithUserId.Value;
            query = query.Where(b => b.BillParticipants.Any(p => p.UserId == withUserId));
        }

        return SortAndPage(ApplyCommonFilters(query, filter), filter);
    }

    public PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null, ProcessingState? status = null)
    {
        return GetForUser(userId, new BillListQuery
        {
            WithUserId = filterByUserId,
            CurrencyCode = filterByCurrencyCode,
            Status = status,
            PageNumber = pageNumber,
            CountPerPage = countPerPage,
            WithIncludesUnfinalized = false,
        });
    }

    public PagingResult<BillListModel> GetForUser(Guid userId, BillListQuery filter)
    {
        var query = _debtContext
            .Bills
            .Where(b => b
                .BillParticipants
                .Any(p => p.UserId == userId));

        query = ApplyCommonFilters(query, filter);

        if (filter.CreatedByMe)
        {
            query = query.Where(b => b.CreatorId == userId);
        }

        if (filter.WithUserId != null)
        {
            var withUserId = filter.WithUserId.Value;
            query = filter.WithIncludesUnfinalized
                ? query.Where(b =>
                    b.LedgerRecords.Any(q => (q.CreditorUserId == withUserId && q.DebtorUserId == userId)
                        || (q.DebtorUserId == withUserId && q.CreditorUserId == userId))
                    || (!b.LedgerRecords.Any() && b.BillParticipants.Any(p => p.UserId == withUserId)))
                : query.Where(b =>
                    b.LedgerRecords.Any(q => (q.CreditorUserId == withUserId && q.DebtorUserId == userId)
                        || (q.DebtorUserId == withUserId && q.CreditorUserId == userId)));
        }

        return SortAndPage(query, filter);
    }

    /// <summary>The filters <c>GET Bills</c> and <c>GET Bills/All</c> share: currency, status, dates and search.</summary>
    private static IQueryable<Bill> ApplyCommonFilters(IQueryable<Bill> query, BillListQuery filter)
    {
        if (filter.CurrencyCode != null)
        {
            query = query.Where(b => b.PaymentCurrencyCode == filter.CurrencyCode);
        }

        if (filter.Status != null)
        {
            query = query.Where(b => b.Status == filter.Status);
        }

        if (filter.DateFrom != null)
        {
            var from = filter.DateFrom.Value;
            query = query.Where(b => b.Date >= from);
        }

        if (filter.DateTo != null)
        {
            var before = filter.DateTo.Value.Date.AddDays(1);
            query = query.Where(b => b.Date < before);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(b => b.Description.ToLower().Contains(search));
        }

        return query;
    }

    private PagingResult<BillListModel> SortAndPage(IQueryable<Bill> query, BillListQuery filter)
    {
        var desc = filter.SortOrder == SortOrder.Desc;
        // A unique last key keeps pages stable when the sort field has equal values.
        var sorted = filter.SortBy switch
        {
            BillSortField.Description => (desc ? query.OrderByDescending(b => b.Description) : query.OrderBy(b => b.Description))
                .ThenByDescending(b => b.Date),
            // Cast for SQLite (tests), which can't order by decimal; Postgres orders the same.
            BillSortField.Total => (desc ? query.OrderByDescending(b => (double)b.TotalWithTips) : query.OrderBy(b => (double)b.TotalWithTips))
                .ThenByDescending(b => b.Date),
            BillSortField.Status => (desc ? query.OrderByDescending(b => b.Status) : query.OrderBy(b => b.Status))
                .ThenByDescending(b => b.Date),
            _ => desc ? query.OrderByDescending(b => b.Date) : query.OrderBy(b => b.Date),
        };

        return sorted
            .ThenBy(b => b.Id)
            .ProjectTo<BillListModel>(_mapper.ConfigurationProvider)
            .ToPagingResult(filter.PageNumber, filter.CountPerPage);
    }

    public PagingResult<ExchangeListItem> GetExchangesForUser(Guid userId, ExchangeListQuery filter)
    {
        // An exchange is two bills linked by ExchangeBillId. Each bill has one payer ("from") and one line
        // participant ("to"), and the two bills mirror each other, so one bill describes the whole exchange.
        // Pick one bill per pair: the one you pay in when you're one of the two people (so "from" is you),
        // otherwise (you only created it) the one with the smaller id.
        // Access is the same as GET Bills: creator, payer or line participant. It's checked on the bills
        // themselves, not on BillParticipants, which is filled in the background just after the bills are created.
        var query = _debtContext
            .Bills
            .Where(b => b.ExchangeBillId != null)
            .Where(b => b.Payments.Any(p => p.UserId == userId)
                || (b.CreatorId == userId
                    && !b.Lines.Any(l => l.Participants.Any(p => p.UserId == userId))
                    && b.Id < b.ExchangeBillId!.Value));

        if (filter.WithUserId != null)
        {
            var withUserId = filter.WithUserId.Value;
            query = query.Where(b => b.Lines.Any(l => l.Participants.Any(p => p.UserId == withUserId))
                || b.Payments.Any(p => p.UserId == withUserId));
        }

        if (filter.CurrencyCode != null)
        {
            var currency = filter.CurrencyCode;
            query = query.Where(b => b.CurrencyCode == currency || b.PaymentCurrencyCode == currency);
        }

        if (filter.Status != null)
        {
            query = filter.Status == ProcessingState.Processed
                ? query.Where(b => b.Status == ProcessingState.Processed || b.Status == ProcessingState.Ready)
                : query.Where(b => b.Status == filter.Status);
        }

        var desc = filter.SortOrder == SortOrder.Desc;
        // Cast for SQLite (tests), which can't order by decimal; Postgres orders the same.
        var sorted = filter.SortBy switch
        {
            ExchangeSortField.Amount => (desc
                    ? query.OrderByDescending(b => b.Payments.Select(p => (double)p.Amount).FirstOrDefault())
                    : query.OrderBy(b => b.Payments.Select(p => (double)p.Amount).FirstOrDefault()))
                .ThenByDescending(b => b.Date),
            _ => desc ? query.OrderByDescending(b => b.Date) : query.OrderBy(b => b.Date),
        };

        return sorted
            .ThenBy(b => b.Id)
            .Select(b => new ExchangeListItem
            {
                ForwardBillId = b.Id,
                BackwardBillId = b.ExchangeBillId!.Value,
                Date = b.Date,
                Status = b.Status,
                Description = b.Description,
                Creator = new UserDisplayModel
                {
                    Id = b.Creator.Id,
                    DisplayName = b.Creator.DisplayName,
                    TelegramId = b.Creator.TelegramId,
                    TelegramUserName = b.Creator.TelegramUserName,
                },
                FromUser = b.Payments
                    .Select(p => new UserDisplayModel
                    {
                        Id = p.User.Id,
                        DisplayName = p.User.DisplayName,
                        TelegramId = p.User.TelegramId,
                        TelegramUserName = p.User.TelegramUserName,
                    })
                    .FirstOrDefault(),
                FromAmount = b.Payments.Select(p => p.Amount).FirstOrDefault(),
                FromCurrencyCode = b.PaymentCurrencyCode,
                ToUser = b.Lines
                    .SelectMany(l => l.Participants)
                    .Select(p => new UserDisplayModel
                    {
                        Id = p.User.Id,
                        DisplayName = p.User.DisplayName,
                        TelegramId = p.User.TelegramId,
                        TelegramUserName = p.User.TelegramUserName,
                    })
                    .FirstOrDefault(),
                ToAmount = b.TotalWithTips,
                ToCurrencyCode = b.CurrencyCode,
            })
            .ToPagingResult(filter.PageNumber, filter.CountPerPage);
    }

    public PagingResult<BillModel> GetCreatedByUser(Guid userId, int pageNumber = 0, int? countPerPage = null)
    {

        var bills = _debtContext
            .Bills
            .OrderByDescending(q => q.Date)
            .Where(q => q.CreatorId == userId)
            .ProjectTo<BillModel>(_mapper.ConfigurationProvider)
            .ToPagingResult<BillModel>(pageNumber, countPerPage);

        return bills;
    }

    public BillLineModel? GetLine(Guid id)
    {
        var billLine = _debtContext
            .BillLines
            .Where(q => q.Id == id)
            .Include(q => q.Participants)
            .ThenInclude(q => q.User)
            .ProjectTo<BillLineModel>(_mapper.ConfigurationProvider)
            .FirstOrDefault();

        return billLine;
    }

    public void SetExchangePair(Guid forwardBillId, Guid backwardBillId)
    {
        var bills = _debtContext.Bills.Where(b => b.Id == forwardBillId || b.Id == backwardBillId).ToList();
        foreach (var bill in bills)
        {
            bill.ExchangeBillId = bill.Id == forwardBillId ? backwardBillId : forwardBillId;
        }
        _debtContext.SaveChanges();
    }

    public async Task SetTotalWithTipsAsync(Guid billId, decimal totalWithTips, CancellationToken cancellationToken)
    {
        var bill = await _debtContext.Bills.FirstOrDefaultAsync(b => b.Id == billId, cancellationToken);
        if (bill is null)
        {
            return;
        }

        bill.TotalWithTips = totalWithTips;
        await _debtContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Adds the bill's participants (creator, line participants and payers) in the same transaction as the bill,
    /// so lists filtered on BillParticipants (GET Bills) show a bill as soon as it's created. They used to be
    /// written later by the EnsureBillParticipant consumer, which made a just-created bill missing from GET Bills.
    /// </summary>
    private void ensureParticipants(Guid billId, IEnumerable<Guid> userIds)
    {
        foreach (var userId in userIds.Distinct())
        {
            if (_debtContext.BillParticipants.Local.Any(p => p.BillId == billId && p.UserId == userId)
                || _debtContext.BillParticipants.Any(p => p.BillId == billId && p.UserId == userId))
            {
                continue;
            }

            _debtContext.BillParticipants.Add(new BillParticipant { BillId = billId, UserId = userId });
        }
    }

    private void addLines(Guid billId, List<BillLineParserModel> parsedLines, UserModel creator)
    {
        parsedLines.ForEach(l => addLine(billId, l, creator));
    }
    public void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserModel creator)
    {
        using var transaction = _debtContext.Database.BeginTransaction();

        addLines(billId, parsedLines, creator);
        ensureParticipants(billId, parsedLines.SelectMany(l => l.Participants).Select(p => p.User.Id!.Value));
        _debtContext.SaveChanges();

        transaction.Commit();
    }

    private void addPayments(Guid billId, List<BillPaymentParserModel> payments, UserModel creator)
    {
        payments.ForEach(p => addPayment(billId, p, creator));
    }

    public void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserModel creator)
    {
        using var transaction = _debtContext.Database.BeginTransaction();

        addPayments(billId, payments, creator);
        ensureParticipants(billId, payments.Select(p => p.User.Id!.Value));
        _debtContext.SaveChanges();

        transaction.Commit();
    }

    public Guid Add(BillParserModel parsedBill, UserModel creator)
    {
        using var transaction = _debtContext.Database.BeginTransaction();

        Bill? bill = new Bill
        {
            CreatorId = creator.Id
        };
        _debtContext.Bills.Add(bill);


        if (parsedBill.CurrencyCode is not null)
        {
            bill.CurrencyCode = parsedBill.CurrencyCode;
        }

        if (parsedBill.PaymentCurrencyCode is not null)
        {
            bill.PaymentCurrencyCode = parsedBill.PaymentCurrencyCode;
        }

        if (parsedBill.Description is not null)
        {
            bill.Description = parsedBill.Description;
        }

        if (parsedBill.Date is not null)
        {
            bill.Date = parsedBill.Date.Value;
        }

        if (parsedBill.TotalWithTips is not null)
        {
            bill.TotalWithTips = parsedBill.TotalWithTips.Value;
        }
        _debtContext.SaveChanges();

        if (parsedBill.Lines is { Count: > 0 })
        {
            addLines(bill.Id, parsedBill.Lines, creator);
        }

        if (parsedBill.Payments is { Count: > 0 })
        {
            addPayments(bill.Id, parsedBill.Payments, creator);
        }

        ensureParticipants(bill.Id, (parsedBill.Lines ?? []).SelectMany(l => l.Participants).Select(p => p.User.Id!.Value)
            .Concat((parsedBill.Payments ?? []).Select(p => p.User.Id!.Value))
            .Prepend(creator.Id));
        _debtContext.SaveChanges();

        transaction.Commit();

        return bill.Id;
    }

    private void addLine(Guid billId, BillLineParserModel parsedLine, UserModel creator)
    {
        BillLine? billLine;
        if (parsedLine.Id is not null)
        {
            billLine = _debtContext.BillLines.FirstOrDefault(q => q.Id == parsedLine.Id.Value)
                   ?? throw new Exception("Bill not found");
        }
        else
        {
            billLine = new BillLine
            {
                BillId = billId
            };
            _debtContext.BillLines.Add(billLine);
        }

        if (parsedLine.ItemDescription is not null)
        {
            billLine.ItemDescription = parsedLine.ItemDescription;
        }

        if (parsedLine.Subtotal.HasValue)
        {
            billLine.Subtotal = parsedLine.Subtotal.Value;
        }

        _debtContext.SaveChanges();

        // add participants
        parsedLine.Participants.ForEach(q => addLineParticipant(billId, billLine.Id, q, creator));

        parsedLine.Id = billLine.Id;
    }

    private void addLineParticipant(Guid billId, Guid billLineId, BillLineParticipantParserModel parsedParticipant, UserModel creator)
    {
        var lineParticipant = _debtContext.BillLineParticipants.FirstOrDefault(q => q.BillLineId == billLineId && q.UserId == parsedParticipant.User.Id!.Value);
        if (lineParticipant is null)
        {
            lineParticipant = new BillLineParticipant()
            {
                BillLineId = billLineId,
                UserId = parsedParticipant.User.Id!.Value
            };
            _debtContext.BillLineParticipants.Add(lineParticipant);
        }

        if (parsedParticipant.Part is not null)
        {
            lineParticipant.Part = parsedParticipant.Part.Value;
        }

        _debtContext.SaveChanges();
    }

    private void addPayment(Guid billId, BillPaymentParserModel parsedPayment, UserModel creator)
    {
        var payment = _debtContext.BillPayments.FirstOrDefault(q => q.BillId == billId && q.UserId == parsedPayment.User.Id!.Value);

        if (payment is null)
        {
            payment = new BillPayment()
            {
                BillId = billId,
                UserId = parsedPayment.User.Id!.Value
            };
            _debtContext.BillPayments.Add(payment);
        }

        if (parsedPayment.Amount is not null)
        {
            payment.Amount = parsedPayment.Amount.Value;
        }

        _debtContext.SaveChanges();
    }

    /// <summary>
    /// Replaces a draft's fields, lines and payments in one transaction (PUT Bills/{id}). The first statement is a
    /// conditional update on the bill row (still a draft, not an exchange), so it takes the row lock: a finalize
    /// running at the same time either went first (nothing is updated and this returns false) or waits for this
    /// transaction and then finalizes the new content. The bill's participants are rebuilt: the creator always
    /// stays, the people on the new lines and payments are added and everyone else is removed.
    /// </summary>
    public bool ReplaceDraft(Guid billId, BillParserModel parsedBill, UserModel creator)
    {
        using var transaction = _debtContext.Database.BeginTransaction();

        var updated = _debtContext.Bills
            .Where(b => b.Id == billId && b.Status == ProcessingState.Draft && b.ExchangeBillId == null)
            .ExecuteUpdate(s => s
                .SetProperty(b => b.CurrencyCode, parsedBill.CurrencyCode!)
                .SetProperty(b => b.PaymentCurrencyCode, parsedBill.PaymentCurrencyCode!)
                .SetProperty(b => b.Description, parsedBill.Description ?? "")
                .SetProperty(b => b.Date, parsedBill.Date!.Value)
                .SetProperty(b => b.TotalWithTips, parsedBill.TotalWithTips!.Value));
        if (updated == 0)
        {
            transaction.Rollback();
            return false;
        }

        _debtContext.BillLineParticipants.Where(p => p.BillLine.BillId == billId).ExecuteDelete();
        _debtContext.BillLines.Where(l => l.BillId == billId).ExecuteDelete();
        _debtContext.BillPayments.Where(p => p.BillId == billId).ExecuteDelete();

        addLines(billId, parsedBill.Lines ?? [], creator);
        addPayments(billId, parsedBill.Payments ?? [], creator);

        var keep = (parsedBill.Lines ?? []).SelectMany(l => l.Participants).Select(p => p.User.Id!.Value)
            .Concat((parsedBill.Payments ?? []).Select(p => p.User.Id!.Value))
            .Prepend(creator.Id)
            .Distinct()
            .ToList();
        _debtContext.BillParticipants.Where(p => p.BillId == billId && !keep.Contains(p.UserId)).ExecuteDelete();
        ensureParticipants(billId, keep);
        _debtContext.SaveChanges();

        transaction.Commit();
        return true;
    }

    public async Task<bool> SetBillStatusAsync(Guid billId, ProcessingState status, CancellationToken cancellationToken)
    {
        var bill = await _debtContext.Bills.FirstOrDefaultAsync(q => q.Id == billId, cancellationToken);
        if (bill == null)
        {
            return false;
        }

        bill.Status = status;
        await _debtContext.SaveChangesAsync();

        return true;
    }
}