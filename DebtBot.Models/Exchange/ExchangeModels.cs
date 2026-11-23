namespace DebtBot.Models.Exchange;

/// <summary>
/// An exchange between two users, the same as Telegram's <c>/ExchangeDebt</c>:
/// <see cref="FromUserId"/> hands over <see cref="FromAmount"/> <see cref="FromCurrencyCode"/>,
/// and <see cref="ToUserId"/> hands back <see cref="ToAmount"/> <see cref="ToCurrencyCode"/>.
/// It is created as two draft bills; accept it with <c>POST Bills/exchange/finalize</c>.
/// </summary>
public class ExchangeCreationModel
{
    public Guid FromUserId { get; set; }
    public decimal FromAmount { get; set; }
    public string FromCurrencyCode { get; set; } = "";

    public Guid ToUserId { get; set; }
    public decimal ToAmount { get; set; }
    public string ToCurrencyCode { get; set; } = "";

    public string? Description { get; set; }
    /// <summary>Defaults to now (UTC).</summary>
    public DateTime? Date { get; set; }
}

/// <summary>The two bills of an exchange.</summary>
public class ExchangeModel
{
    /// <summary>The bill in which the "from" user pays: afterwards the "to" user owes them <c>FromAmount FromCurrencyCode</c>.</summary>
    public Guid ForwardBillId { get; set; }
    /// <summary>The bill in which the "to" user pays: afterwards the "from" user owes them <c>ToAmount ToCurrencyCode</c>.</summary>
    public Guid BackwardBillId { get; set; }
}
