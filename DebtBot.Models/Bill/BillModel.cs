using DebtBot.Models.Enums;
using DebtBot.Models.BillLine;

namespace DebtBot.Models.Bill;

public class BillModel
{
    public Guid id { get; set; }
    public Guid CreatorId { get; set; }
    public string CurrencyCode { get; set; }
    public string PaymentCurrencyCode { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; }
    public ProcessingState Status { get; set; }
    public decimal TotalWithTips { get; set; }

    public List<BillLineModel> Lines { get; set; }
    public List<BillPaymentModel> Payments { get; set; }
}