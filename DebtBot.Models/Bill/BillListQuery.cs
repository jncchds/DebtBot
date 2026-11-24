using DebtBot.Models.Enums;

namespace DebtBot.Models.Bill;

/// <summary>Filters, sort and paging for the bills a user takes part in (<c>GET Bills</c>).</summary>
public class BillListQuery
{
    /// <summary>Only bills shared with this user: a ledger record between the two of you, or, before the bill has a ledger (draft, finalizing), both of you take part.</summary>
    public Guid? WithUserId { get; set; }
    /// <summary>Only bills paid in this currency (upper case).</summary>
    public string? CurrencyCode { get; set; }
    public ProcessingState? Status { get; set; }
    /// <summary>Only bills you created.</summary>
    public bool CreatedByMe { get; set; }
    /// <summary>Only bills on or after this date (UTC).</summary>
    public DateTime? DateFrom { get; set; }
    /// <summary>Only bills before the end of this day (UTC).</summary>
    public DateTime? DateTo { get; set; }
    /// <summary>Description contains this text, ignoring case.</summary>
    public string? Search { get; set; }

    public BillSortField SortBy { get; set; } = BillSortField.Date;
    public SortOrder SortOrder { get; set; } = SortOrder.Desc;

    public int PageNumber { get; set; }
    public int? CountPerPage { get; set; }

    /// <summary>
    /// False keeps the bot's older meaning of <see cref="WithUserId"/>: only a ledger record between the two users.
    /// </summary>
    public bool WithIncludesUnfinalized { get; set; } = true;
}
