using DebtBot.DB.Enums;
using DebtBot.Models.BillLine;

namespace DebtBot.Models.Bill;

public class BillCreationModel
{
    public Guid CreatorId { get; set; }
    public string CurrencyCode { get; set; }
    public string PaymentCurrencyCode { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; }
    public ProcessingState Status { get; set; }
    public decimal Total { get; set; }

    public List<BillLineCreationModel> Lines { get; set; }
    public List<BillPaymentModel> Payments { get; set; }
}
