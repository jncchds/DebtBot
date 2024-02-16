using DebtBot.Models.BillLineParticipant;

namespace DebtBot.Models.BillLine;

public class BillLineModel
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    public string ItemDescription { get; set; }
    public decimal Subtotal { get; set; }

    public List<BillLineParticipantModel> Participants { get; set; }
}