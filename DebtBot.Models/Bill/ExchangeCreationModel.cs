namespace DebtBot.Models.Bill;

public class ExchangeCreationModel
{
    public Guid FromUserId { get; set; }
    public string FromCurrencyCode { get; set; }
    public decimal FromAmount { get; set; }
    public Guid ToUserId { get; set; }
    public string ToCurrencyCode { get; set; }
    public decimal ToAmount { get; set; }
    public string Description { get; set; }
}
