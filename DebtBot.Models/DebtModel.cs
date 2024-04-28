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
        return (Amount < 0) ?
            $"{CreditorUser} owes {DebtorUser} {Amount:0.##} {CurrencyCode}" :
            $"{DebtorUser} owes {CreditorUser} {Amount:0.##} {CurrencyCode}";
    }
}
