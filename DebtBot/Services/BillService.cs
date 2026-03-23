using AutoMapper;
using DebtBot.DB;
using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;
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

    public void AddLines(Guid billId, List<BillLineCreationModel> lines, Guid creatorId)
    {
        var creator = _userService.GetUserById(creatorId)
            ?? throw new Exception("creator not found");

        var parserLines = lines.Select(l => new BillLineParserModel
        {
            ItemDescription = l.ItemDescription,
            Subtotal = l.Subtotal,
            Participants = l.Participants.Select(p => new BillLineParticipantParserModel
            {
                Part = p.Part,
                User = new UserSearchModel { Id = p.UserId }
            }).ToList()
        }).ToList();

        var creatorSearch = new UserSearchModel { Id = creatorId };
        fillLineParticipantUserIds(parserLines, creator);
        _billRepository.AddLines(billId, parserLines, creator);
    }

    public void AddPayments(Guid billId, List<BillPaymentCreationModel> payments, Guid creatorId)
    {
        var creator = _userService.GetUserById(creatorId)
            ?? throw new Exception("creator not found");

        var parserPayments = payments.Select(p => new BillPaymentParserModel
        {
            Amount = p.Amount,
            User = new UserSearchModel { Id = p.UserId }
        }).ToList();

        fillPaymentUserIds(parserPayments, creator);
        _billRepository.AddPayments(billId, parserPayments, creator);
    }

    public ExchangeResultModel AddExchange(ExchangeCreationModel exchangeModel, Guid creatorId)
    {
        var creatorSearch = new UserSearchModel { Id = creatorId };

        var fromUser = new UserSearchModel { Id = exchangeModel.FromUserId };
        var toUser = new UserSearchModel { Id = exchangeModel.ToUserId };

        var forwardBill = new BillParserModel
        {
            Description = $"{exchangeModel.Description}\nExchange {exchangeModel.FromAmount / exchangeModel.ToAmount:0.##} {exchangeModel.FromCurrencyCode}/{exchangeModel.ToCurrencyCode}",
            CurrencyCode = exchangeModel.ToCurrencyCode,
            PaymentCurrencyCode = exchangeModel.FromCurrencyCode,
            TotalWithTips = exchangeModel.ToAmount,
            Date = DateTime.UtcNow,
            Lines = new List<BillLineParserModel>
            {
                new BillLineParserModel
                {
                    ItemDescription = "Exchange",
                    Subtotal = exchangeModel.ToAmount,
                    Participants = new List<BillLineParticipantParserModel>
                    {
                        new BillLineParticipantParserModel { Part = 1, User = toUser }
                    }
                }
            },
            Payments = new List<BillPaymentParserModel>
            {
                new BillPaymentParserModel { Amount = exchangeModel.FromAmount, User = fromUser }
            }
        };

        var backwardBill = new BillParserModel
        {
            Description = $"{exchangeModel.Description}\nExchange {exchangeModel.ToAmount / exchangeModel.FromAmount:0.##} {exchangeModel.ToCurrencyCode}/{exchangeModel.FromCurrencyCode}",
            CurrencyCode = exchangeModel.FromCurrencyCode,
            PaymentCurrencyCode = exchangeModel.ToCurrencyCode,
            TotalWithTips = exchangeModel.FromAmount,
            Date = DateTime.UtcNow,
            Lines = new List<BillLineParserModel>
            {
                new BillLineParserModel
                {
                    ItemDescription = "Exchange",
                    Subtotal = exchangeModel.FromAmount,
                    Participants = new List<BillLineParticipantParserModel>
                    {
                        new BillLineParticipantParserModel { Part = 1, User = fromUser }
                    }
                }
            },
            Payments = new List<BillPaymentParserModel>
            {
                new BillPaymentParserModel { Amount = exchangeModel.ToAmount, User = toUser }
            }
        };

        var forwardBillId = Add(forwardBill, creatorSearch);
        var backwardBillId = Add(backwardBill, creatorSearch);

        return new ExchangeResultModel
        {
            ForwardBillId = forwardBillId,
            BackwardBillId = backwardBillId
        };
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

    public PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null)
    {
        return _billRepository.GetForUser(userId, filterByUserId, filterByCurrencyCode, pageNumber, countPerPage);
    }

    public Guid Add(BillCreationModel billModel, Guid creatorId)
    {
        return _billRepository.Add(billModel, creatorId);
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