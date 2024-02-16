namespace DebtBot.Models.BillLineParticipant;

public class BillLineParticipantModel
{
    public Guid BillLineId { get; set; }
    public Guid UserId { get; set; }
    public decimal Part { get; set; }
    public string UserDisplayName { get; set; }
}