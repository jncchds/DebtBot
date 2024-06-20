using DebtBot.Models.Enums;
using DebtBot.Models.LedgerRecord;

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
    public List<LedgerRecordModel> LedgerRecords { get; set; }
}
