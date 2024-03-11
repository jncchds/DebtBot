using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IBillService
{
    BillModel? Get(Guid id);
    List<BillModel> Get();
    PagingResult<BillModel> GetForUser(Guid userId, int pageNumber = 0, int? countPerPage = null);
    Guid Add(BillCreationModel billModel, Guid creatorId);
    Guid Add(string billString, Guid creatorId);
    Guid Add(BillParserModel parsedBill, UserSearchModel creator);
    Guid Add(BillImportModel bill, UserModel creator);
    bool Finalize(Guid id);
    List<BillModel> GetCreatedByUser(Guid userId);
    bool HasAccess(Guid userId, BillModel? bill);

    BillLineModel? GetLine(Guid id);
    void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserSearchModel creatorSearchModel);

    void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserSearchModel creatorSearchModel);
}