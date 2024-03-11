namespace DebtBot.Models.Bill;

public class BillLineImportModel
{
	public Guid? Id { get; set; }
	public string? ItemDescription { get; set; }
	public decimal? Subtotal { get; set; }

	public List<BillLineParticipantImportModel> Participants { get; set; }
}