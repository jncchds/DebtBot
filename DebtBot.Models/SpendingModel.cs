using DebtBot.Models.User;

namespace DebtBot.Models;
public class SpendingModel
{
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }
    public decimal PaymentAmount { get; set; }
    public string PaymentCurrencyCode { get; set; }
    public UserDisplayModel User { get; set; }

    public override string ToString()
    {
        return $"{Amount:0.##} {CurrencyCode} / {PaymentAmount:0.##} {PaymentCurrencyCode} - {User}";
    }
}
