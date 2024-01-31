using DebtBot.Models.BillLineParticipant;

namespace DebtBot.Models.BillLine;

public class BillLineCreationModel
{
    public string ItemDescription { get; set; }
    public decimal Subtotal { get; set; }

    public List<BillLineParticipantCreationModel> Participants { get; set; }
}
