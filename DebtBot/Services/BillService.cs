using AutoMapper;
using DebtBot.DB;
using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
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
        // No transaction here: the repository opens its own, and a nested BeginTransaction throws.
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

    public async Task<bool> FinalizeAsync(Guid id, CancellationToken cancellationToken, bool forceSponsor = false)
    {
        var bill = await _billRepository.GetAsync(id, cancellationToken);
        if (bill == null)
        {
            return false;
        }

        if (bill.Status != ProcessingState.Draft)
        {
            return false;
        }

        var success = await _billRepository.SetBillStatusAsync(id, ProcessingState.Ready, cancellationToken);

        if (success)
        {
            await _publishEndpoint.Publish(new BillFinalized(id, forceSponsor), cancellationToken);
        }

        return success;
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        await _publishEndpoint.Publish(new BillCanceled(id), cancellationToken);
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

    public async Task<BillModel?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _billRepository.GetAsync(id, cancellationToken);
    }

    public PagingResult<BillModel> Get(int pageNumber = 0, int? countPerPage = null)
    {
        return _billRepository.Get(pageNumber, countPerPage);
    }

    public PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null, ProcessingState? status = null)
    {
        return _billRepository.GetForUser(userId, filterByUserId, filterByCurrencyCode, pageNumber, countPerPage, status);
    }

    public bool CanChange(Guid userId, bool isAdmin, BillModel bill)
    {
        return isAdmin || bill.Creator?.Id == userId;
    }

    private async Task<BillActionResult<BillModel>> getForChangeAsync(Guid billId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var bill = await _billRepository.GetAsync(billId, cancellationToken);
        if (bill is null || (!isAdmin && !HasAccess(userId, bill)))
        {
            return BillActionResult<BillModel>.NotFound();
        }

        if (!CanChange(userId, isAdmin, bill))
        {
            return BillActionResult<BillModel>.Forbidden();
        }

        return BillActionResult<BillModel>.Ok(bill);
    }

    private List<string> unknownUsers(IEnumerable<Guid> userIds)
    {
        return userIds
            .Where(id => id != Guid.Empty && _userService.GetUserById(id) is null)
            .Select(id => $"Unknown user {id}")
            .ToList();
    }

    private async Task ensureParticipantsAsync(Guid billId, IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds.Distinct())
        {
            await _publishEndpoint.Publish(new EnsureBillParticipant(billId, userId), cancellationToken);
        }
    }

    public BillActionResult<Guid> Create(BillCreationModel billModel, Guid creatorId)
    {
        var errors = BillRules.ValidateBill(billModel);
        if (errors.Count == 0)
        {
            errors.AddRange(unknownUsers(BillRules.UserIds(billModel.Lines, billModel.Payments)));
        }
        if (errors.Count != 0)
        {
            return BillActionResult<Guid>.Invalid(errors);
        }

        var billId = Add(BillRules.ToParserModel(billModel, DateTime.UtcNow), new UserSearchModel { Id = creatorId });

        return BillActionResult<Guid>.Ok(billId);
    }

    public async Task<BillActionResult<BillModel>> AddLinesAsync(Guid billId, List<BillLineCreationModel> lines, Guid userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var access = await getForChangeAsync(billId, userId, isAdmin, cancellationToken);
        if (access.Status != BillActionStatus.Ok)
        {
            return access;
        }
        var bill = access.Value!;

        var errors = new List<string>();
        if (bill.Status != ProcessingState.Draft)
            errors.Add("Only draft bills can be changed");
        if ((lines ?? []).Count == 0)
            errors.Add("No lines given");
        if (errors.Count == 0)
            errors.AddRange(BillRules.ValidateLines(lines!));
        if (errors.Count == 0)
            errors.AddRange(unknownUsers(BillRules.UserIds(lines, null)));
        if (errors.Count != 0)
        {
            return BillActionResult<BillModel>.Invalid(errors);
        }

        var creator = _userService.GetUserById(userId)!;
        var parsedLines = BillRules.ToParserLines(lines!);

        // A bill whose total is just the sum of its lines (no tips, or nothing yet) keeps following the lines.
        var linesSum = (bill.Lines ?? []).Sum(l => l.Subtotal);
        var newTotal = bill.TotalWithTips == linesSum
            ? linesSum + parsedLines.Sum(l => l.Subtotal ?? 0m)
            : (decimal?)null;

        _billRepository.AddLines(billId, parsedLines, creator);
        if (newTotal is not null)
        {
            await _billRepository.SetTotalWithTipsAsync(billId, newTotal.Value, cancellationToken);
        }

        await ensureParticipantsAsync(billId, BillRules.UserIds(lines, null), cancellationToken);

        return BillActionResult<BillModel>.Ok((await _billRepository.GetAsync(billId, cancellationToken))!);
    }

    public async Task<BillActionResult<BillModel>> AddPaymentsAsync(Guid billId, List<BillPaymentCreationModel> payments, Guid userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var access = await getForChangeAsync(billId, userId, isAdmin, cancellationToken);
        if (access.Status != BillActionStatus.Ok)
        {
            return access;
        }
        var bill = access.Value!;

        var errors = new List<string>();
        if (bill.Status != ProcessingState.Draft)
            errors.Add("Only draft bills can be changed");
        if ((payments ?? []).Count == 0)
            errors.Add("No payments given");
        if (errors.Count == 0)
            errors.AddRange(BillRules.ValidatePayments(payments!));
        if (errors.Count == 0)
            errors.AddRange(unknownUsers(BillRules.UserIds(null, payments)));
        if (errors.Count != 0)
        {
            return BillActionResult<BillModel>.Invalid(errors);
        }

        var creator = _userService.GetUserById(userId)!;
        _billRepository.AddPayments(billId, BillRules.ToParserPayments(payments!), creator);

        await ensureParticipantsAsync(billId, BillRules.UserIds(null, payments), cancellationToken);

        return BillActionResult<BillModel>.Ok((await _billRepository.GetAsync(billId, cancellationToken))!);
    }

    public async Task<BillActionResult<BillModel>> FinalizeAsync(Guid billId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var access = await getForChangeAsync(billId, userId, isAdmin, cancellationToken);
        if (access.Status != BillActionStatus.Ok)
        {
            return access;
        }

        var errors = BillRules.ValidateForFinalize(access.Value!);
        if (errors.Count != 0)
        {
            return BillActionResult<BillModel>.Invalid(errors);
        }

        if (!await FinalizeAsync(billId, cancellationToken))
        {
            return BillActionResult<BillModel>.Invalid("The bill could not be finalized");
        }

        return BillActionResult<BillModel>.Ok(access.Value!);
    }

    public async Task<BillActionResult<BillModel>> CancelAsync(Guid billId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var access = await getForChangeAsync(billId, userId, isAdmin, cancellationToken);
        if (access.Status != BillActionStatus.Ok)
        {
            return access;
        }
        var bill = access.Value!;

        if (bill.Status == ProcessingState.Cancelled)
        {
            return BillActionResult<BillModel>.Invalid("The bill is already cancelled");
        }

        await CancelAsync(billId, cancellationToken);

        // The two bills of an exchange go together.
        if (bill.ExchangeBillId is not null)
        {
            var pair = await getForChangeAsync(bill.ExchangeBillId.Value, userId, isAdmin, cancellationToken);
            if (pair.Status == BillActionStatus.Ok && pair.Value!.Status != ProcessingState.Cancelled)
            {
                await CancelAsync(pair.Value.Id, cancellationToken);
            }
        }

        return BillActionResult<BillModel>.Ok(bill);
    }

    public async Task<BillActionResult<BillLineModel>> GetLineAsync(Guid lineId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var line = _billRepository.GetLine(lineId);
        if (line is null)
        {
            return BillActionResult<BillLineModel>.NotFound();
        }

        var bill = await _billRepository.GetAsync(line.BillId, cancellationToken);
        if (bill is null || (!isAdmin && !HasAccess(userId, bill)))
        {
            return BillActionResult<BillLineModel>.NotFound();
        }

        return BillActionResult<BillLineModel>.Ok(line);
    }

    public BillActionResult<ExchangeModel> CreateExchange(ExchangeCreationModel exchange, Guid creatorId)
    {
        var errors = ExchangeRules.Validate(exchange);
        if (errors.Count == 0)
        {
            errors.AddRange(unknownUsers([exchange.FromUserId, exchange.ToUserId]));
        }
        if (errors.Count != 0)
        {
            return BillActionResult<ExchangeModel>.Invalid(errors);
        }

        var (forwardBill, backwardBill) = ExchangeRules.BuildBills(exchange, DateTime.UtcNow);
        var creator = new UserSearchModel { Id = creatorId };

        var forwardBillId = Add(forwardBill, creator);
        var backwardBillId = Add(backwardBill, creator);
        SetExchangePair(forwardBillId, backwardBillId);

        return BillActionResult<ExchangeModel>.Ok(new ExchangeModel { ForwardBillId = forwardBillId, BackwardBillId = backwardBillId });
    }

    public void SetExchangePair(Guid forwardBillId, Guid backwardBillId)
    {
        _billRepository.SetExchangePair(forwardBillId, backwardBillId);
    }

    public async Task<BillActionResult<ExchangeModel>> FinalizeExchangeAsync(Guid forwardBillId, Guid backwardBillId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var forward = await getForChangeAsync(forwardBillId, userId, isAdmin, cancellationToken);
        var backward = await getForChangeAsync(backwardBillId, userId, isAdmin, cancellationToken);

        if (forward.Status == BillActionStatus.NotFound || backward.Status == BillActionStatus.NotFound)
        {
            return BillActionResult<ExchangeModel>.NotFound();
        }
        if (forward.Status == BillActionStatus.Forbidden || backward.Status == BillActionStatus.Forbidden)
        {
            return BillActionResult<ExchangeModel>.Forbidden();
        }

        var errors = ExchangeRules.ValidatePair(forward.Value!, backward.Value!);
        if (errors.Count == 0)
        {
            errors.AddRange(BillRules.ValidateForFinalize(forward.Value!).Select(e => $"Forward bill: {e}"));
            errors.AddRange(BillRules.ValidateForFinalize(backward.Value!).Select(e => $"Backward bill: {e}"));
        }
        if (errors.Count != 0)
        {
            return BillActionResult<ExchangeModel>.Invalid(errors);
        }

        // Same as the Finalize button of FinalizeExchangeCommand: finalize both bills.
        if (!await FinalizeAsync(forwardBillId, cancellationToken))
            errors.Add("The forward bill could not be finalized");
        if (!await FinalizeAsync(backwardBillId, cancellationToken))
            errors.Add("The backward bill could not be finalized");
        if (errors.Count != 0)
        {
            return BillActionResult<ExchangeModel>.Invalid(errors);
        }

        return BillActionResult<ExchangeModel>.Ok(new ExchangeModel { ForwardBillId = forwardBillId, BackwardBillId = backwardBillId });
    }

    public PagingResult<BillModel> GetCreatedByUser(Guid userId, int pageNumber = 0, int? countPerPage = null)
    {
        return _billRepository.GetCreatedByUser(userId, pageNumber, countPerPage);
    }

    public BillLineModel? GetLine(Guid id)
    {
        return _billRepository.GetLine(id);
    }
}