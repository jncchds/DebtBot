using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IBillService
{
    Task<BillModel?> GetAsync(Guid id, CancellationToken cancellationToken);
    PagingResult<BillModel> Get(int pageNumber = 0, int? countPerPage = null);
    PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null);
    Guid Add(BillCreationModel billModel, Guid creatorId);
    Guid Add(BillParserModel parsedBill, UserSearchModel creator);
    Task<bool> FinalizeAsync(Guid id, CancellationToken cancelationToken, bool forceSponsor = false);
    Task CancelAsync(Guid id, CancellationToken cancelationToken);
    PagingResult<BillModel> GetCreatedByUser(Guid userId, int pageNumber = 0, int? countPerPage = null);
    bool HasAccess(Guid userId, BillModel? bill);

    BillLineModel? GetLine(Guid id);
    void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserSearchModel creatorSearchModel);

    void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserSearchModel creatorSearchModel);
}