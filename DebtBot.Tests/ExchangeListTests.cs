using DebtBot.DB.Entities;
using DebtBot.Models;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
using DebtBot.Services;

namespace DebtBot.Tests;

/// <summary><c>GET Bills/exchanges</c>: one row per pair, seen from your side, with filters, sorting and paging (T6).</summary>
public sealed class ExchangeListTests : IDisposable
{
    private readonly SqliteFixture _f = new();
    private readonly User _anna;
    private readonly User _boris;
    private readonly User _clara;
    private readonly User _dmitry;

    public ExchangeListTests()
    {
        _anna = _f.AddUser("Anna");
        _boris = _f.AddUser("Boris");
        _clara = _f.AddUser("Clara");
        _dmitry = _f.AddUser("Dmitry");
    }

    public void Dispose() => _f.Dispose();

    private static DateTime Day(int d) => new(2026, 11, d, 12, 0, 0, DateTimeKind.Utc);

    private ExchangeModel AddExchange(User creator, User from, decimal fromAmount, string fromCurrency, User to, decimal toAmount, string toCurrency,
        DateTime date, ProcessingState status = ProcessingState.Draft)
    {
        var result = _f.BillService.CreateExchange(new ExchangeCreationModel
        {
            FromUserId = from.Id, FromAmount = fromAmount, FromCurrencyCode = fromCurrency,
            ToUserId = to.Id, ToAmount = toAmount, ToCurrencyCode = toCurrency,
            Date = date,
        }, creator.Id);
        Assert.Equal(BillActionStatus.Ok, result.Status);
        _f.Clear();
        foreach (var bill in _f.Db.Bills.Where(b => b.Id == result.Value!.ForwardBillId || b.Id == result.Value.BackwardBillId))
            bill.Status = status;
        _f.Db.SaveChanges();
        _f.Clear();
        return result.Value!;
    }

    private PagingResult<ExchangeListItem> List(User viewer, ExchangeListQuery? q = null) =>
        _f.BillRepository.GetExchangesForUser(viewer.Id, q ?? new ExchangeListQuery());

    [Fact]
    public void OneRowPerPair_SeenFromYourSide()
    {
        // Boris created it: Boris gives 430 PLN, Anna gives back 100 EUR.
        var pair = AddExchange(_boris, _boris, 430m, "PLN", _anna, 100m, "EUR", Day(22));

        var anna = Assert.Single(List(_anna).Items);
        Assert.Equal(_anna.Id, anna.FromUser!.Id);
        Assert.Equal(100m, anna.FromAmount);
        Assert.Equal("EUR", anna.FromCurrencyCode);
        Assert.Equal(_boris.Id, anna.ToUser!.Id);
        Assert.Equal(430m, anna.ToAmount);
        Assert.Equal("PLN", anna.ToCurrencyCode);
        Assert.Equal(_boris.Id, anna.Creator.Id);
        Assert.Equal(ProcessingState.Draft, anna.Status);
        Assert.Equal(100m / 430m, anna.Rate);
        Assert.Equal(new HashSet<Guid> { pair.ForwardBillId, pair.BackwardBillId }, new HashSet<Guid> { anna.ForwardBillId, anna.BackwardBillId });

        var boris = Assert.Single(List(_boris).Items);
        Assert.Equal(_boris.Id, boris.FromUser!.Id);
        Assert.Equal(430m, boris.FromAmount);
        Assert.Equal(anna.BackwardBillId, boris.ForwardBillId);

        Assert.Empty(List(_clara).Items);
    }

    [Fact]
    public void CreatorWhoIsNotInIt_SeesItOnce()
    {
        AddExchange(_anna, _boris, 50m, "USD", _clara, 200m, "PLN", Day(19));

        Assert.Single(List(_anna).Items);
        Assert.Single(List(_boris).Items);
        Assert.Single(List(_clara).Items);
        Assert.Empty(List(_dmitry).Items);
    }

    [Fact]
    public void OrdinaryBillsAreNotExchanges()
    {
        var bill = _f.BillService.Create(new Models.Bill.BillCreationModel
        {
            CurrencyCode = "EUR",
            Lines = [new Models.BillLine.BillLineCreationModel { ItemDescription = "Pizza", Subtotal = 10m, Participants = [new() { UserId = _boris.Id }] }],
            Payments = [new BillPaymentCreationModel { UserId = _anna.Id, Amount = 10m }],
        }, _anna.Id);
        Assert.Equal(BillActionStatus.Ok, bill.Status);

        Assert.Empty(List(_anna).Items);
    }

    [Fact]
    public void Filters()
    {
        AddExchange(_anna, _anna, 100m, "EUR", _boris, 430m, "PLN", Day(22));
        AddExchange(_dmitry, _dmitry, 200m, "PLN", _anna, 50m, "USD", Day(19));
        AddExchange(_anna, _anna, 46m, "EUR", _clara, 50m, "USD", Day(10), ProcessingState.Processed);
        AddExchange(_anna, _anna, 70m, "EUR", _boris, 300m, "PLN", Day(8), ProcessingState.Ready);
        AddExchange(_anna, _anna, 10m, "GBP", _clara, 12m, "EUR", Day(5), ProcessingState.Cancelled);

        List<DateTime> dates(ExchangeListQuery q) => List(_anna, q).Items.Select(x => x.Date).ToList();

        Assert.Equal([Day(22), Day(8)], dates(new ExchangeListQuery { WithUserId = _boris.Id }));
        Assert.Equal([Day(19), Day(10)], dates(new ExchangeListQuery { CurrencyCode = "USD" }));
        Assert.Equal([Day(22), Day(19)], dates(new ExchangeListQuery { Status = ProcessingState.Draft }));
        // Accepted includes Ready (still being written).
        Assert.Equal([Day(10), Day(8)], dates(new ExchangeListQuery { Status = ProcessingState.Processed }));
        Assert.Equal([Day(5)], dates(new ExchangeListQuery { Status = ProcessingState.Cancelled }));
        Assert.Equal([Day(8)], dates(new ExchangeListQuery { WithUserId = _boris.Id, Status = ProcessingState.Processed }));
    }

    [Fact]
    public void Sorts_ByDate_AndByTheAmountYouGive_WithStablePaging()
    {
        AddExchange(_anna, _anna, 100m, "EUR", _boris, 430m, "PLN", Day(22));
        AddExchange(_dmitry, _dmitry, 200m, "PLN", _anna, 50m, "USD", Day(19)); // Anna gives 50
        AddExchange(_anna, _anna, 46m, "EUR", _clara, 50m, "USD", Day(10));
        AddExchange(_anna, _anna, 46m, "EUR", _boris, 200m, "PLN", Day(9));

        List<decimal> amounts(ExchangeListQuery q) => List(_anna, q).Items.Select(x => x.FromAmount).ToList();

        Assert.Equal([100m, 50m, 46m, 46m], amounts(new ExchangeListQuery()));
        Assert.Equal([46m, 46m, 50m, 100m], amounts(new ExchangeListQuery { SortOrder = SortOrder.Asc }));
        Assert.Equal([100m, 50m, 46m, 46m], amounts(new ExchangeListQuery { SortBy = ExchangeSortField.Amount }));
        Assert.Equal([46m, 46m, 50m, 100m], amounts(new ExchangeListQuery { SortBy = ExchangeSortField.Amount, SortOrder = SortOrder.Asc }));

        var all = List(_anna, new ExchangeListQuery { SortBy = ExchangeSortField.Amount }).Items.Select(x => x.ForwardBillId).ToList();
        var paged = Enumerable.Range(0, 4)
            .SelectMany(p => List(_anna, new ExchangeListQuery { SortBy = ExchangeSortField.Amount, PageNumber = p, CountPerPage = 1 }).Items)
            .Select(x => x.ForwardBillId).ToList();
        Assert.Equal(all, paged);
        Assert.Equal(4, List(_anna, new ExchangeListQuery { CountPerPage = 1 }).TotalCount);
    }
}
