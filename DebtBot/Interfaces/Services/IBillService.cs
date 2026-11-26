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
    /// <summary>Every bill (admin, <c>GET Bills/All</c>).</summary>
    PagingResult<BillListModel> GetAll(AdminBillListQuery filter);
    PagingResult<BillListModel> GetForUser(Guid userId, Guid? filterByUserId, string? filterByCurrencyCode, int pageNumber = 0, int? countPerPage = null, ProcessingState? status = null);
    PagingResult<BillListModel> GetForUser(Guid userId, BillListQuery filter);
    /// <summary>Exchanges you created or take part in, one row per pair of bills (<c>GET Bills/exchanges</c>).</summary>
    PagingResult<ExchangeListItem> GetExchangesForUser(Guid userId, ExchangeListQuery filter);
    Guid Add(BillParserModel parsedBill, UserSearchModel creator);
    Task<bool> FinalizeAsync(Guid id, CancellationToken cancelationToken, bool forceSponsor = false);
    Task CancelAsync(Guid id, CancellationToken cancelationToken);
    PagingResult<BillModel> GetCreatedByUser(Guid userId, int pageNumber = 0, int? countPerPage = null);
    bool HasAccess(Guid userId, BillModel? bill);
    /// <summary>
    /// Read access (<c>GET Bills/{id}</c>, its lines): <see cref="HasAccess"/>, or a confirmed subscription to anyone who takes
    /// part in the bill. Changes still need <see cref="HasAccess"/> and <see cref="CanChange"/>.
    /// </summary>
    bool CanRead(Guid userId, BillModel? bill);
    /// <summary>Changes to a bill are for its creator or an admin.</summary>
    bool CanChange(Guid userId, bool isAdmin, BillModel bill);
    /// <summary>
    /// For the bot: whether this Telegram user may change the bill (its creator or an admin). An unknown bill counts
    /// as allowed, so the command reports it the way it always has.
    /// </summary>
    Task<bool> CanTelegramUserChangeAsync(Guid billId, long telegramUserId, CancellationToken cancellationToken);
    /// <summary>Cancelling a bill is for its creator only (not admins).</summary>
    bool CanCancel(Guid userId, BillModel bill);
    /// <summary>For the bot: whether this Telegram user created the bill. An unknown bill counts as allowed.</summary>
    Task<bool> CanTelegramUserCancelAsync(Guid billId, long telegramUserId, CancellationToken cancellationToken);

    BillLineModel? GetLine(Guid id);
    void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserSearchModel creatorSearchModel);

    void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserSearchModel creatorSearchModel);

    // Structured (web) operations, with access checks for the calling user.
    BillActionResult<Guid> Create(BillCreationModel billModel, Guid creatorId);
    /// <summary>
    /// Replaces a draft's fields, lines and payments (creator or admin). 404 without access, 403 for other people,
    /// 400 for an exchange or invalid input, 409 when the bill is no longer a draft.
    /// </summary>
    Task<BillActionResult<BillModel>> ReplaceDraftAsync(Guid billId, BillCreationModel billModel, Guid userId, bool isAdmin, CancellationToken cancellationToken);
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