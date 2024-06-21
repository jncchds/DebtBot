using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Extensions;
using DebtBot.Interfaces.Repositories;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.Enums;
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

    public BillModel? Get(Guid id)
    {
        var bill = _debtContext
            .Bills
            .Where(q => q.Id == id)
            .ProjectTo<BillModel>(_mapper.ConfigurationProvider)
            .FirstOrDefault();

        return bill;
    }

    public List<BillModel> Get()
    {
        var bills = _debtContext
            .Bills
            .OrderByDescending(q => q.Date)
            .ProjectTo<BillModel>(_mapper.ConfigurationProvider)
            .ToList();
        return bills;
    }

    public PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null)
    {
        var query = _debtContext
            .Bills
            .Include(t => t.LedgerRecords)
            .ThenInclude(t => t.CreditorUser)
            .Include(t => t.LedgerRecords)
            .ThenInclude(t => t.DebtorUser)
            .OrderByDescending(q => q.Date)
            .Where(b => b
                .BillParticipants
                .Any(p => p.UserId == userId));

        if (filterByCurrencyCode != null)
        {
            query = query.Where(b => b.PaymentCurrencyCode == filterByCurrencyCode);
        }

        if (filterByUserId != null)
        {
            query = query.Where(b => b.LedgerRecords
                .Any(q => (q.CreditorUserId == filterByUserId && q.DebtorUserId == userId) 
                    || (q.DebtorUserId == filterByUserId && q.CreditorUserId == userId)));
        }

        var bills = query.ProjectTo<BillListModel>(_mapper.ConfigurationProvider)
            .ToPagingResult(pageNumber, countPerPage);

        return bills;
    }

    public List<BillModel> GetCreatedByUser(Guid userId)
    {

        var bills = _debtContext
            .Bills
            .OrderByDescending(q => q.Date)
            .Where(q => q.CreatorId == userId)
            .ProjectTo<BillModel>(_mapper.ConfigurationProvider)
            .ToList();

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

    public Guid Add(BillCreationModel billModel, Guid creatorId)
    {
        var bill = _mapper.Map<Bill>(billModel);

        bill.CreatorId = creatorId;
        _debtContext.Bills.Add(bill);
        _debtContext.SaveChanges();

        return bill.Id;
    }

    private void addLines(Guid billId, List<BillLineParserModel> parsedLines, UserModel creator)
    {
        parsedLines.ForEach(l => addLine(billId, l, creator));
    }
    public void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserModel creator)
    {
        using var transaction = _debtContext.Database.BeginTransaction();

        addLines(billId, parsedLines, creator);
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

        if (!parsedBill.Lines.IsNullOrEmpty())
        {
            addLines(bill.Id, parsedBill.Lines, creator);
        }

        if (!parsedBill.Payments.IsNullOrEmpty())
        {
            addPayments(bill.Id, parsedBill.Payments, creator);
        }

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

    public bool SetBillStatus(Guid billId, ProcessingState status)
    {
        var bill = _debtContext.Bills.FirstOrDefault(q => q.Id == billId);
        if (bill == null)
        {
            return false;
        }

        bill.Status = status;
        _debtContext.SaveChanges();

        return true;
    }
}