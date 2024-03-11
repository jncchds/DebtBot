using DebtBot.Models.User;

namespace DebtBot.Models.Bill;

public class BillImportModel
{
	public Guid? Id { get; set; }
	public string? CurrencyCode { get; set; }
	public string? PaymentCurrencyCode { get; set; }
	public string? Description { get; set; }
	public DateTime? Date { get; set; }
	public decimal? TotalWithTips { get; set; }

	public Guid Creator { get; set; }
	public List<BillLineImportModel> Lines { get; set; }
	public List<BillPaymentImportModel> Payments { get; set; }
}