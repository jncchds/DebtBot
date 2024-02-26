using DebtBot.Models.BillLineParticipant;
using System.Text;

namespace DebtBot.Models.BillLine;

public class BillLineModel
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    public string ItemDescription { get; set; }
    public decimal Subtotal { get; set; }

    public List<BillLineParticipantModel> Participants { get; set; }

    public void AppendToStringBuilder(StringBuilder sb, string? currencyCode = null, decimal tipAdjustment = 1.0m)
    {
        sb.AppendLine($"<b>{ItemDescription}</b>");
        sb.AppendLine($"<b>Subtotal:</b> {Subtotal:0.##} {currencyCode}");

        var totalParts = Participants.Sum(t => t.Part);
        if (totalParts == 0.0m)
        {
            totalParts = 1.0m;
        }

        if ((Participants ?? []).Count != 0)
        {
            sb.AppendLine($"<b>Participants:</b>");
            Participants!.ForEach(participant =>
            {
                sb.Append($"  {participant.Part * 100.0m / totalParts:0.##}%");
                sb.Append($" - {(participant.Part * Subtotal * tipAdjustment / totalParts):0.##}");
                if (!string.IsNullOrEmpty(currencyCode))
                {
                    sb.Append($" {currencyCode}");
                }
                sb.AppendLine($" - {participant.User}");
            });
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.AppendLine($"<span class=\"tg-spoiler\">BillLine {Id}</span>");
        AppendToStringBuilder(sb);
        return sb.ToString();
    }
}