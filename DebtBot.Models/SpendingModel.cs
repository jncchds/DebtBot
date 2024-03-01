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

    private string spendingString
    {
        get
        {
            if (string.Equals(CurrencyCode, PaymentCurrencyCode, StringComparison.InvariantCultureIgnoreCase))
                return $"{Amount:0.##} {CurrencyCode}";
            else
                return $"{Amount:0.##} {CurrencyCode} | {PaymentAmount:0.##} {PaymentCurrencyCode}";
        }
    }

    public string ToBillString()
    {
        return $"{Portion * 100.0m:00.00}% | {spendingString} | {User}";
    }

    public string ToNotificationString()
    {
        return $"{Portion * 100.0m:0.##}% of the bill\n{spendingString}";
    }

    public override string ToString()
    {
        return $"{Description}\n    {Portion * 100.0m:0.##}% of the bill\n    {spendingString}";
    }
}
