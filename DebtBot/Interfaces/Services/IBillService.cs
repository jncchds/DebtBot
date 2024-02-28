using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IBillService
{
    BillModel? Get(Guid id);
    List<BillModel> Get();
    List<BillModel> GetForUser(Guid userId);
    Guid Add(BillCreationModel billModel, Guid creatorId);
    Guid Add(string billString, Guid creatorId);
    Guid Add(BillParserModel parsedBill, UserSearchModel creator);
    bool Finalize(Guid id);
    List<BillModel> GetByUser(Guid userId);
    bool HasAccess(Guid userId, BillModel? bill);

    BillLineModel? GetLine(Guid id);
    void AddLines(Guid billId, List<BillLineParserModel> parsedLines, UserSearchModel creatorSearchModel);

    void AddPayments(Guid billId, List<BillPaymentParserModel> payments, UserSearchModel creatorSearchModel);
}