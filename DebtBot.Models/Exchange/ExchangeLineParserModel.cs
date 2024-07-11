using DebtBot.Models.User;

namespace DebtBot.Models.Exchange;
public class ExchangeLineParserModel
{
    public ExchangeAmountType AmountType { get; set; }
    public decimal Value { get; set; }
    public string CurrencyCode { get; set; }
    public UserSearchModel User { get; set; }
}