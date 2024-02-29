using DebtBot.Models.Enums;
using DebtBot.Models.BillLine;
using DebtBot.Models.User;
using System.Text;

namespace DebtBot.Models.Bill;

public class BillModel
{
    public Guid Id { get; set; }
    public UserDisplayModel Creator { get; set; }
    public string CurrencyCode { get; set; }
    public string PaymentCurrencyCode { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; }
    public ProcessingState Status { get; set; }
    public decimal TotalWithTips { get; set; }

    public List<BillLineModel> Lines { get; set; }
    public List<BillPaymentModel> Payments { get; set; }

    public List<SpendingModel> Spendings { get; set; }

    public override string ToString()
    {
        decimal totalWithoutTips = Lines?.Sum(t => t.Subtotal) ?? TotalWithTips;
        if (totalWithoutTips == 0.0m)
        {
            totalWithoutTips = TotalWithTips;
        }
        decimal tipAdjustment = TotalWithTips / totalWithoutTips;

        StringBuilder sb = new();

        sb.AppendLine($"<span class=\"tg-spoiler\">Bill {Id}</span>");
        sb.AppendLine($"<b>Description:</b> {Description}");
        sb.AppendLine();
        sb.AppendLine($"<b>Creator:</b> {Creator}");
        sb.AppendLine($"<b>Created:</b> {Date}");
        sb.AppendLine($"<b>Total with tips:</b> {TotalWithTips:0.##} {CurrencyCode}");
        sb.AppendLine($"<b>Tips:</b> {(tipAdjustment - 1m) * 100.0m:0.##}%");
        sb.AppendLine($"<b>Paid:</b> {(Payments?.Sum(t => t.Amount) ?? 0.0m):0.##} {PaymentCurrencyCode}");
        sb.AppendLine($"<b>Status:</b> {Status}");

        if ((Payments ?? []).Count != 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Payments:</b>");
            Payments!.ForEach(payment =>
            {
                sb.AppendLine($"  {payment.Amount:0.##} {PaymentCurrencyCode} by {payment.User}");
            });
        }
        if ((Lines ?? []).Count != 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Lines:</b>");
            Lines!.ForEach(line =>
            {
                sb.AppendLine();
                line.AppendToStringBuilder(sb, CurrencyCode, tipAdjustment);
            });
        }
        if ((Spendings ?? []).Count != 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Spendings:</b>");
            Spendings!.ForEach(spending => sb.AppendLine($"  {spending.ToBillString()}"));
        }

        return sb.ToString();
    }
}