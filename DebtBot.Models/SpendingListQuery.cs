using DebtBot.Models.Enums;

namespace DebtBot.Models;

/// <summary>Filters, sort and paging for your own spendings (<c>GET Spendings/Own</c>).</summary>
public class SpendingListQuery
{
    /// <summary>Whose list: a user you have a confirmed subscription to, or yourself. Left out = yours.</summary>
    public Guid? OfUserId { get; set; }

    /// <summary>Only spendings in this bill currency (upper case).</summary>
    public string? CurrencyCode { get; set; }
    /// <summary>Only spendings on or after this date (UTC).</summary>
    public DateTime? DateFrom { get; set; }
    /// <summary>Only spendings before the end of this day (UTC).</summary>
    public DateTime? DateTo { get; set; }
    /// <summary>Also list spendings of cancelled bills (off by default).</summary>
    public bool IncludeCancelled { get; set; }

    public SpendingSortField SortBy { get; set; } = SpendingSortField.Date;
    public SortOrder SortOrder { get; set; } = SortOrder.Desc;

    public int PageNumber { get; set; }
    public int? CountPerPage { get; set; }
}

/// <summary>Your total spending in one bill currency (<c>GET Spendings/Own/summary</c>).</summary>
public class SpendingSummaryModel
{
    public string CurrencyCode { get; set; } = "";
    public decimal Amount { get; set; }
    public int Count { get; set; }
}
