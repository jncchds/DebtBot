using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.Enums;
using DebtBot.Models.User;

namespace DebtBot.Repositories;
public interface IBillRepository
{
    Guid Add(BillCreationModel billModel, Guid creatorId);
    Guid Add(BillParserModel parsedBill, UserModel creator);
    void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserModel creator);
    void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserModel creator);
    List<BillModel> Get();
    Task<BillModel?> GetAsync(Guid id, CancellationToken cancellationToken);
    List<BillModel> GetCreatedByUser(Guid userId);
    PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null);
    BillLineModel? GetLine(Guid id);
    Task<bool> SetBillStatusAsync(Guid billId, ProcessingState status, CancellationToken cancellationToken);
}