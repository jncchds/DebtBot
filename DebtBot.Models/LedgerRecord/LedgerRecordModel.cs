using DebtBot.Models.User;

namespace DebtBot.Models.LedgerRecord;
public class LedgerRecordModel
{
    public UserDisplayModel CreditorUser { get; set; }
    public UserDisplayModel DebtorUser { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }
}
