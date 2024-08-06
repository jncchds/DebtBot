using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IBillService
{
    Task<BillModel?> GetAsync(Guid id, CancellationToken cancellationToken);
    List<BillModel> Get();
    PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null);
    Guid Add(BillCreationModel billModel, Guid creatorId);
    Guid Add(BillParserModel parsedBill, UserSearchModel creator);
    Task<bool> FinalizeAsync(Guid id, CancellationToken cancelationToken, bool forceSponsor = false);
    Task CancelAsync(Guid id, CancellationToken cancelationToken);
    List<BillModel> GetCreatedByUser(Guid userId);
    bool HasAccess(Guid userId, BillModel? bill);

    BillLineModel? GetLine(Guid id);
    void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserSearchModel creatorSearchModel);

    void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserSearchModel creatorSearchModel);
}