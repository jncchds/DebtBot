using DebtBot.Models.User;

namespace DebtBot.Models.Bill;

public class BillPaymentParserModel
{
	public decimal? Amount { get; set; }
	public UserSearchModel User { get; set; }
}