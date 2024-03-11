using DebtBot.Models.User;

namespace DebtBot.Models.Bill;

public class BillPaymentImportModel
{
	public decimal? Amount { get; set; }
	public Guid UserId { get; set; }
}