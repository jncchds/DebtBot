using DebtBot.Models.Enums;
using DebtBot.Models.LedgerRecord;
using DebtBot.Models.User;

namespace DebtBot.Models.Bill;
public class BillListModel
{
    public Guid Id { get; set; }
    public string CurrencyCode { get; set; }
    public string PaymentCurrencyCode { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; }
    public ProcessingState Status { get; set; }
    public decimal TotalWithTips { get; set; }
    public UserDisplayModel Creator { get; set; }
    /// <summary>For the two bills of an exchange: the id of the other bill of the pair.</summary>
    public Guid? ExchangeBillId { get; set; }
    public List<LedgerRecordModel> LedgerRecords { get; set; }
}
