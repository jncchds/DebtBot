using DebtBot.DB.Entities;

namespace DebtBot.Models;

public class BillModel
{
	public Guid id { get; set; }
	public string CurrencyCode { get; set; }
	public string Description { get; set; }
	public DateTime Date { get; set; }
	
	public List<BillLineModel> Lines { get; set; }
	public List<BillPaymentModel> Payments { get; set; }
}