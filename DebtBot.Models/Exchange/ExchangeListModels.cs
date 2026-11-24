using DebtBot.Models.Enums;
using DebtBot.Models.User;

namespace DebtBot.Models.Exchange;

/// <summary>Filters, sort and paging for <c>GET Bills/exchanges</c>.</summary>
public class ExchangeListQuery
{
    /// <summary>Only exchanges with this user on the other side.</summary>
    public Guid? WithUserId { get; set; }
    /// <summary>Only exchanges where either side's currency is this one (upper case).</summary>
    public string? CurrencyCode { get; set; }
    /// <summary><c>Draft</c> = waiting to accept; <c>Processed</c> = accepted (includes <c>Ready</c>, still being written); <c>Cancelled</c>.</summary>
    public ProcessingState? Status { get; set; }

    public ExchangeSortField SortBy { get; set; } = ExchangeSortField.Date;
    public SortOrder SortOrder { get; set; } = SortOrder.Desc;

    public int PageNumber { get; set; }
    public int? CountPerPage { get; set; }
}

/// <summary>
/// One exchange (both of its bills) as one row. "From" hands over <see cref="FromAmount"/> <see cref="FromCurrencyCode"/>
/// and "to" hands back <see cref="ToAmount"/> <see cref="ToCurrencyCode"/>. When you're one of the two people, "from" is you.
/// </summary>
public class ExchangeListItem
{
    /// <summary>The bill in which "from" pays. Send both ids to <c>POST Bills/exchange/finalize</c> to accept.</summary>
    public Guid ForwardBillId { get; set; }
    /// <summary>The bill in which "to" pays.</summary>
    public Guid BackwardBillId { get; set; }
    public DateTime Date { get; set; }
    public ProcessingState Status { get; set; }
    public string Description { get; set; } = "";
    public UserDisplayModel Creator { get; set; } = null!;

    public UserDisplayModel? FromUser { get; set; }
    public decimal FromAmount { get; set; }
    public string FromCurrencyCode { get; set; } = "";

    public UserDisplayModel? ToUser { get; set; }
    public decimal ToAmount { get; set; }
    public string ToCurrencyCode { get; set; } = "";

    /// <summary><see cref="FromAmount"/> / <see cref="ToAmount"/>: how much of the "from" currency one unit of the "to" currency costs.</summary>
    public decimal Rate => ToAmount == 0 ? 0 : FromAmount / ToAmount;
}
