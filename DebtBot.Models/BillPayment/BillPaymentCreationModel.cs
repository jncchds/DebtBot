using DebtBot.Models.User;

namespace DebtBot.Models;

public class BillPaymentCreationModel
{
	public Guid UserId { get; set; }
    
	public decimal Amount { get; set; }
}