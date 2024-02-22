using DebtBot.Models.User;

namespace DebtBot.Models.Bill;

public class BillLineParticipantParserModel
{
	public decimal? Part { get; set; }
	public UserSearchModel User { get; set; }
}