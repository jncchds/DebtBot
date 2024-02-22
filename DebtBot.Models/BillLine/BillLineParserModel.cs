namespace DebtBot.Models.Bill;

public class BillLineParserModel
{
	public Guid? Id { get; set; }
	public string? ItemDescription { get; set; }
	public decimal? Subtotal { get; set; }

	public List<BillLineParticipantParserModel> Participants { get; set; }
}