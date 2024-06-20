using AutoMapper;
using DebtBot.DB;
using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using DebtBot.Repositories;
using MassTransit;
using Microsoft.IdentityModel.Tokens;

namespace DebtBot.Services;

public class BillService : IBillService
{
    private readonly DebtContext _debtContext;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IUserService _userService;
    private readonly IBillRepository _billRepository;

    public BillService(
        DebtContext debtContext,
        IMapper mapper,
        IPublishEndpoint publishEndpoint,
        IUserService userService,
        IBillRepository billRepository)
    {
        _debtContext = debtContext;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
        _userService = userService;
        _billRepository = billRepository;
    }

    public void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserSearchModel creatorSearchModel)
    { 
        var creator = _userService.FindUser(creatorSearchModel);

        if (creator is null)
        {
            throw new Exception("creator not found");
        }

        _billRepository.AddLines(billId, parsedLines, creator);
    }

    public void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserSearchModel creatorSearchModel)
    {
        using var transaction = _debtContext.Database.BeginTransaction();

        var creator = _userService.FindUser(creatorSearchModel);
        if (creator is null)
        {
            throw new Exception("creator not found");
        }

        _billRepository.AddPayments(billId, payments, creator);
    }

    private void fillLineParticipantUserIds(List<BillLineParserModel> parsedLines, UserModel creator)
    {
        parsedLines.ForEach(l => fillLineParticipantUserIds(l, creator));
    }

    private void fillPaymentUserIds(List<BillPaymentParserModel> payments, UserModel creator)
    {
        payments.ForEach(p => fillPaymentUserId(p, creator));
    }

    private void fillLineParticipantUserIds(BillLineParserModel parsedLine, UserModel creator)
    {
        parsedLine.Participants.ForEach(q => fillLineParticipantUserId(q, creator));
    }

    private void fillLineParticipantUserId(BillLineParticipantParserModel parsedParticipant, UserModel creator)
    {
        var lineUser = _userService.FindOrAddUser(parsedParticipant.User, creator);

        parsedParticipant.User.Id = lineUser.Id;
    }

    private void fillPaymentUserId(BillPaymentParserModel parsedPayment, UserModel creator)
    {
        var paymentUser = _userService.FindOrAddUser(parsedPayment.User, creator);

        parsedPayment.User.Id = paymentUser.Id;
    }

    public Guid Add(BillParserModel parsedBill, UserSearchModel creatorSearchModel)
    {
        var creator = _userService.FindUser(creatorSearchModel);
        if (creator is null)
        {
            throw new Exception("creator not found");
        }
        
        if (!parsedBill.Lines.IsNullOrEmpty())
        {
            fillLineParticipantUserIds(parsedBill.Lines, creator);
        }

        if (!parsedBill.Payments.IsNullOrEmpty())
        {
            fillPaymentUserIds(parsedBill.Payments, creator);
        }

        var billId = _billRepository.Add(parsedBill, creator);

        _ = Task.Run(() =>
        {
            _publishEndpoint.Publish(new EnsureBillParticipant(billId, creator.Id));

            parsedBill.Lines?.All(t => t.Participants?.All(p =>
            {
                _publishEndpoint.Publish(new EnsureBillParticipant(billId, p.User.Id!.Value));
                return true;
            }) ?? true);

            parsedBill.Payments?.All(p =>
            {
                _publishEndpoint.Publish(new EnsureBillParticipant(billId, p.User.Id!.Value));
                return true;
            });
        });

        return billId;
    }

    public bool Finalize(Guid id, bool forceSponsor = false)
    {
        var bill = _billRepository.Get(id);
        if (bill == null)
        {
            return false;
        }

        if (bill.Status != ProcessingState.Draft)
        {
            return false;
        }

        var ret = _billRepository.SetBillStatus(id, ProcessingState.Ready);

        if (ret)
        {
            _publishEndpoint.Publish(new BillFinalized(id, forceSponsor));
        }

        return ret;
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

    public BillModel? Get(Guid id)
    {
        return _billRepository.Get(id);
    }

    public List<BillModel> Get()
    {
        return _billRepository.Get();
    }

    public PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null)
    {
        return _billRepository.GetForUser(userId, filterByUserId, filterByCurrencyCode, pageNumber, countPerPage);
    }

    public Guid Add(BillCreationModel billModel, Guid creatorId)
    {
        return _billRepository.Add(billModel, creatorId);
    }

    public List<BillModel> GetCreatedByUser(Guid userId)
    {
        return _billRepository.GetCreatedByUser(userId);
    }

    public BillLineModel? GetLine(Guid id)
    {
        return _billRepository.GetLine(id);
    }
}