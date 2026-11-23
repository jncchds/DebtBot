namespace DebtBot.Models.BillLineParticipant;

public class BillLineParticipantCreationModel
{
    public Guid UserId { get; set; }
    /// <summary>The participant's share of the line, above zero. Leave it out (null) for an equal split: it then counts as 1.</summary>
    public decimal? Part { get; set; }
}
