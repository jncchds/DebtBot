namespace DebtBot.Models;

public class BillPaymentCreationModel
{
	public Guid UserId { get; set; }

	/// <summary>Amount paid in the payment currency; above zero.</summary>
	public decimal Amount { get; set; }
}
