using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
using DebtBot.Models.User;
using DebtBot.Services;

namespace DebtBot.Interfaces.Services;

public interface IBillService
{
    Task<BillModel?> GetAsync(Guid id, CancellationToken cancellationToken);
    PagingResult<BillModel> Get(int pageNumber = 0, int? countPerPage = null);
    PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null, ProcessingState? status = null);
    Guid Add(BillParserModel parsedBill, UserSearchModel creator);
    Task<bool> FinalizeAsync(Guid id, CancellationToken cancelationToken, bool forceSponsor = false);
    Task CancelAsync(Guid id, CancellationToken cancelationToken);
    PagingResult<BillModel> GetCreatedByUser(Guid userId, int pageNumber = 0, int? countPerPage = null);
    bool HasAccess(Guid userId, BillModel? bill);
    /// <summary>Changes to a bill are for its creator or an admin.</summary>
    bool CanChange(Guid userId, bool isAdmin, BillModel bill);

    BillLineModel? GetLine(Guid id);
    void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserSearchModel creatorSearchModel);

    void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserSearchModel creatorSearchModel);

    // Structured (web) operations, with access checks for the calling user.
    BillActionResult<Guid> Create(BillCreationModel billModel, Guid creatorId);
    Task<BillActionResult<BillModel>> AddLinesAsync(Guid billId, List<BillLineCreationModel> lines, Guid userId, bool isAdmin, CancellationToken cancellationToken);
    Task<BillActionResult<BillModel>> AddPaymentsAsync(Guid billId, List<BillPaymentCreationModel> payments, Guid userId, bool isAdmin, CancellationToken cancellationToken);
    Task<BillActionResult<BillModel>> FinalizeAsync(Guid billId, Guid userId, bool isAdmin, CancellationToken cancellationToken);
    Task<BillActionResult<BillModel>> CancelAsync(Guid billId, Guid userId, bool isAdmin, CancellationToken cancellationToken);
    Task<BillActionResult<BillLineModel>> GetLineAsync(Guid lineId, Guid userId, bool isAdmin, CancellationToken cancellationToken);
    BillActionResult<ExchangeModel> CreateExchange(ExchangeCreationModel exchange, Guid creatorId);
    Task<BillActionResult<ExchangeModel>> FinalizeExchangeAsync(Guid forwardBillId, Guid backwardBillId, Guid userId, bool isAdmin, CancellationToken cancellationToken);
    /// <summary>Links the two bills of an exchange to each other.</summary>
    void SetExchangePair(Guid forwardBillId, Guid backwardBillId);
}