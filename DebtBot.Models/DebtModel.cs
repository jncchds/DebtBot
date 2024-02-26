using DebtBot.Models.User;

namespace DebtBot.Models;

public class DebtModel
{
    public UserDisplayModel CreditorUser { get; set; }
    public UserDisplayModel DebtorUser { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }

    public override string ToString()
    {
        return $"{DebtorUser} owes {CreditorUser} {Amount} {CurrencyCode}";
    }

    public string ToCreditorString()
    {
        return $"{DebtorUser} owes you {Amount:0.##} {CurrencyCode}";
    }
}
