using DebtBot.Models.Enums;
using DebtBot.Models.BillLine;

namespace DebtBot.Models.Bill;

public class BillCreationModel
{
    public string CurrencyCode { get; set; }
    public string PaymentCurrencyCode { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; }
    public ProcessingState Status { get; set; }
    public decimal TotalWithTips { get; set; }

    public List<BillLineCreationModel> Lines { get; set; }
    public List<BillPaymentCreationModel> Payments { get; set; }
}
