using DebtBot.Models.User;

namespace DebtBot.Models;
public class SpendingModel
{
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }
    public decimal PaymentAmount { get; set; }
    public string PaymentCurrencyCode { get; set; }
    public decimal Portion { get; set; }
    public Guid BillId { get; set; }
    public UserDisplayModel User { get; set; }

    public string ToBillString()
    {
        return $"{Portion * 100.0m:00.00}% | {Amount:0.##} {CurrencyCode} | {PaymentAmount:0.##} {PaymentCurrencyCode} - {User}";
    }

    public override string ToString()
    {
        return $"{Description}\n    {Portion * 100.0m:0.##}% of the bill\n    {Amount:0.##} {CurrencyCode} | {PaymentAmount:0.##} {PaymentCurrencyCode}";
    }
}
