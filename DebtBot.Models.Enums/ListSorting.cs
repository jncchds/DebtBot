namespace DebtBot.Models.Enums;

/// <summary>Sort direction of a paged list (query parameter <c>sortOrder=Asc|Desc</c>).</summary>
public enum SortOrder : byte
{
    Asc = 0,
    Desc = 1,
}

/// <summary>Fields <c>GET Bills</c> can sort by (query parameter <c>sortBy</c>).</summary>
public enum BillSortField : byte
{
    Date = 0,
    Description = 1,
    Total = 2,
    Status = 3,
}

/// <summary>Fields <c>GET Debts/Own</c> can sort by (query parameter <c>sortBy</c>).</summary>
public enum DebtSortField : byte
{
    Person = 0,
    Amount = 1,
    Currency = 2,
}

/// <summary>Which side of a balance to list in <c>GET Debts/Own</c> (query parameter <c>direction</c>).</summary>
public enum DebtDirection : byte
{
    All = 0,
    /// <summary>The other person owes you (amount &gt; 0).</summary>
    OwedToMe = 1,
    /// <summary>You owe the other person (amount &lt; 0).</summary>
    IOwe = 2,
}

/// <summary>Fields <c>GET Bills/exchanges</c> can sort by (query parameter <c>sortBy</c>).</summary>
public enum ExchangeSortField : byte
{
    Date = 0,
    /// <summary>The amount the "from" side hands over: yours, when you're in the exchange.</summary>
    Amount = 1,
}
