using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DebtBot.Services;

public class BillService : IBillService
{
    private readonly DebtContext _debtContext;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IParserService _parserService;
    private readonly IUserService _userService;
    private readonly IUserContactService _userContactService;

    public BillService(
        DebtContext debtContext,
        IMapper mapper,
        IPublishEndpoint publishEndpoint,
        IParserService parserService,
        IUserService userService,
        IUserContactService userContactService)
    {
        _debtContext = debtContext;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
        _parserService = parserService;
        _userService = userService;
        _userContactService = userContactService;
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

    public PagingResult<BillModel> GetForUser(Guid userId, int pageNumber = 0, int? countPerPage = null)
    {
        var bills = _debtContext
            .Bills
            .OrderByDescending(q => q.Date)
            .Where(b => b
                .BillParticipants
                .Any(p => p.UserId == userId))
            .ProjectTo<BillModel>(_mapper.ConfigurationProvider)
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

    public Guid Add(string billString, Guid creatorId)
    {
        BillCreationModel billModel = _parserService.ParseBill(creatorId, billString);
        return Add(billModel, creatorId);
    }

    private void addLines(Guid billId, List<BillLineParserModel> parsedLines, UserModel creator)
    {
        parsedLines.ForEach(l => addLine(billId, l, creator));
    }
    public void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserSearchModel creatorSearchModel)
    {
        using var transaction = _debtContext.Database.BeginTransaction();

        var creator = _userService.FindUser(creatorSearchModel);
        if (creator is null)
        {
            throw new Exception("creator not found");
        }

        addLines(billId, parsedLines, creator);
        _debtContext.SaveChanges();

        transaction.Commit();
    }

    private void addPayments(Guid billId, List<BillPaymentParserModel> payments, UserModel creator)
    {
        payments.ForEach(p => addPayment(billId, p, creator));
    }

    public void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserSearchModel creatorSearchModel)
    {
        using var transaction = _debtContext.Database.BeginTransaction();

        var creator = _userService.FindUser(creatorSearchModel);
        if (creator is null)
        {
            throw new Exception("creator not found");
        }

        addPayments(billId, payments, creator);
        _debtContext.SaveChanges();

        transaction.Commit();
    }

    public Guid Add(BillParserModel parsedBill, UserSearchModel creatorSearchModel)
    {
        using var transaction = _debtContext.Database.BeginTransaction();

        var creator = _userService.FindUser(creatorSearchModel);
        if (creator is null)
        {
            throw new Exception("creator not found");
        }

        Bill? bill;
        if (parsedBill.Id is not null)
        {
            bill = _debtContext.Bills.FirstOrDefault(q => q.Id == parsedBill.Id.Value)
                   ?? throw new Exception("Bill not found");
        }
        else
        {
            bill = new Bill
            {
                CreatorId = creator.Id
            };
            _debtContext.Bills.Add(bill);
        }

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

        transaction.Commit();

        using var transaction2 = _debtContext.Database.BeginTransaction();

        _publishEndpoint.Publish(new EnsureBillParticipant(bill.Id, creator.Id));
        
        if (!parsedBill.Lines.IsNullOrEmpty())
        {
            addLines(bill.Id, parsedBill.Lines, creator);
        }

        if (!parsedBill.Payments.IsNullOrEmpty())
        {
            addPayments(bill.Id, parsedBill.Payments, creator);
        }

        _debtContext.SaveChanges();

        transaction2.Commit();

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
    }

    private UserModel dealWithUser(UserSearchModel model, UserModel creator)
    {
        var lineUser = _userService.AddUser(model);

        _userContactService.AddContact(creator.Id, lineUser);
        _userContactService.AddContact(lineUser.Id, creator);

        return lineUser;
    }

    private void addLineParticipant(Guid billId, Guid billLineId, BillLineParticipantParserModel parsedParticipant, UserModel creator)
    {
        var lineUser = _userService.FindUser(parsedParticipant.User, creator.Id);
        lineUser ??= dealWithUser(parsedParticipant.User, creator);

        var lineParticipant = _debtContext.BillLineParticipants.FirstOrDefault(q => q.BillLineId == billLineId && q.UserId == lineUser.Id);
        if (lineParticipant is null)
        {
            lineParticipant = new BillLineParticipant()
            {
                BillLineId = billLineId,
                UserId = lineUser.Id
            };
            _debtContext.BillLineParticipants.Add(lineParticipant);
        }

        if (parsedParticipant.Part is not null)
        {
            lineParticipant.Part = parsedParticipant.Part.Value;
        }

        _debtContext.SaveChanges();

        _publishEndpoint.Publish(new EnsureBillParticipant(billId, lineUser.Id));
    }

    private void addPayment(Guid billId, BillPaymentParserModel parsedPayment, UserModel creator)
    {
        var paymentUser = _userService.FindUser(parsedPayment.User, creator.Id);
        paymentUser ??= dealWithUser(parsedPayment.User, creator);

        var payment = _debtContext.BillPayments.FirstOrDefault(q => q.BillId == billId && q.UserId == paymentUser.Id);

        if (payment is null)
        {
            payment = new BillPayment()
            {
                BillId = billId,
                UserId = paymentUser.Id
            };
            _debtContext.BillPayments.Add(payment);
        }

        if (parsedPayment.Amount is not null)
        {
            payment.Amount = parsedPayment.Amount.Value;
        }

        _debtContext.SaveChanges();

        _publishEndpoint.Publish(new EnsureBillParticipant(billId, paymentUser.Id));
    }

    public bool Finalize(Guid id)
    {
        var bill = _debtContext.Bills.FirstOrDefault(q => q.Id == id);
        if (bill == null)
        {
            return false;
        }

        if (bill.Status != ProcessingState.Draft)
        {
            return false;
        }

        bill.Status = ProcessingState.Ready;
        _debtContext.SaveChanges();

        _publishEndpoint.Publish(new BillFinalized(id));

        return true;
    }

    public bool HasAccess(Guid userId, BillModel? bill)
    {
        if (bill is null)
        {
            return false;
        }

        if (bill.Creator.Id == userId)
        {
            return true;
        }

        if (bill.Payments.Any(q => q.User.Id == userId))
        {
            return true;
        }

        if (bill.Lines.Any(q => q.Participants.Any(w => w.User.Id == userId)))
        {
            return true;
        }

        return false;
    }
}