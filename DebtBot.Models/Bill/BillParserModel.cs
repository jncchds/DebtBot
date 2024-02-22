using DebtBot.Models.User;

namespace DebtBot.Models.Bill;

public class BillParserModel
{
	public Guid? Id { get; set; }
	public string? CurrencyCode { get; set; }
	public string? PaymentCurrencyCode { get; set; }
	public string? Description { get; set; }
	public DateTime? Date { get; set; }
	public decimal? TotalWithTips { get; set; }

	public UserSearchModel Creator { get; set; }
	public List<BillLineParserModel> Lines { get; set; }
	public List<BillPaymentParserModel> Payments { get; set; }
}