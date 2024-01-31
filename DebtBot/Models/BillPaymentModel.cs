namespace DebtBot.Models;

public class BillPaymentModel
{
	public Guid UserId { get; set; }
    
	public decimal Amount { get; set; }
}