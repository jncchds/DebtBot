namespace DebtBot.Models;

public class BillPaymentModel
{
	public Guid BillId { get; set; }
	public Guid UserId { get; set; }
    
	public decimal Amount { get; set; }
}