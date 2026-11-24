using DebtBot.Models.Enums;

namespace DebtBot.Models.Debt;

/// <summary>Filters, sort and paging for your own balances (<c>GET Debts/Own</c>).</summary>
public class DebtListQuery
{
    /// <summary>Whose list: a user you have a confirmed subscription to, or yourself. Left out = yours.</summary>
    public Guid? OfUserId { get; set; }

    /// <summary>Only balances with this user.</summary>
    public Guid? WithUserId { get; set; }
    /// <summary>Only balances in this currency (upper case).</summary>
    public string? CurrencyCode { get; set; }
    public DebtDirection Direction { get; set; } = DebtDirection.All;

    public DebtSortField SortBy { get; set; } = DebtSortField.Person;
    public SortOrder SortOrder { get; set; } = SortOrder.Asc;

    public int PageNumber { get; set; }
    public int? CountPerPage { get; set; }
}
