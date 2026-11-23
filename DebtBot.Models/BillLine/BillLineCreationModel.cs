using DebtBot.Models.BillLineParticipant;

namespace DebtBot.Models.BillLine;

public class BillLineCreationModel
{
    public string ItemDescription { get; set; }
    /// <summary>Subtotal without tips in the bill currency; above zero.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>At least one participant, each user at most once.</summary>
    public List<BillLineParticipantCreationModel> Participants { get; set; }
}
