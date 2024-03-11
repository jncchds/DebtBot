using DebtBot.Models.User;

namespace DebtBot.Models.Bill;

public class BillLineParticipantImportModel
{
	public decimal? Part { get; set; }
	public Guid UserId { get; set; }
}