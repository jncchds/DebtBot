namespace DebtBot.Models.Bill;

/// <summary>
/// Filters, sort and paging for every bill (<c>GET Bills/All</c>, admin): <see cref="BillListQuery"/>'s filters, where
/// <see cref="BillListQuery.WithUserId"/> is anyone taking part, plus the creator. <c>OfUserId</c> and <c>CreatedByMe</c>
/// are ignored.
/// </summary>
public class AdminBillListQuery : BillListQuery
{
    /// <summary>Only bills this user created.</summary>
    public Guid? CreatorId { get; set; }
}
