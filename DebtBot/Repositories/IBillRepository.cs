using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
using DebtBot.Models.User;

namespace DebtBot.Repositories;
public interface IBillRepository
{
    void SetExchangePair(Guid forwardBillId, Guid backwardBillId);
    Task SetTotalWithTipsAsync(Guid billId, decimal totalWithTips, CancellationToken cancellationToken);
    Guid Add(BillParserModel parsedBill, UserModel creator);
    void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserModel creator);
    void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserModel creator);
    PagingResult<BillModel> Get(int pageNumber = 0, int? countPerPage = null);
    Task<BillModel?> GetAsync(Guid id, CancellationToken cancellationToken);
    PagingResult<BillModel> GetCreatedByUser(Guid userId, int pageNumber = 0, int? countPerPage = null);
    PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null, ProcessingState? status = null);
    PagingResult<BillListModel> GetForUser(Guid userId, BillListQuery filter);
    /// <summary>Exchanges (pairs of linked bills) you created or are one of the two people in, one row per pair.</summary>
    PagingResult<ExchangeListItem> GetExchangesForUser(Guid userId, ExchangeListQuery filter);
    BillLineModel? GetLine(Guid id);
    Task<bool> SetBillStatusAsync(Guid billId, ProcessingState status, CancellationToken cancellationToken);
}