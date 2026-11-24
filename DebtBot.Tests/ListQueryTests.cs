using DebtBot.DB.Entities;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;
using DebtBot.Models.Debt;
using DebtBot.Models.Enums;
using DebtBot.Services;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Tests;

/// <summary>Filters, sorting and paging of <c>GET Bills</c> and <c>GET Debts/Own</c> (T5).</summary>
public sealed class ListQueryTests : IDisposable
{
    private readonly SqliteFixture _f = new();
    private readonly User _anna;
    private readonly User _boris;
    private readonly User _clara;
    private readonly DebtService _debts;

    public ListQueryTests()
    {
        _anna = _f.AddUser("Anna");
        _boris = _f.AddUser("Boris");
        _clara = _f.AddUser("Clara");
        _debts = new DebtService(_f.Db, _f.Mapper);

        // EnsureCreated makes no views; this mirrors the Postgres "debts" materialized view.
        _f.Db.Database.ExecuteSqlRaw(@"
create view debts as
select CreditorUserId, DebtorUserId, CurrencyCode, sum(Amount) as Amount
from (
    select CreditorUserId, DebtorUserId, Amount, CurrencyCode from LedgerRecords where IsCanceled = 0
    union all
    select DebtorUserId, CreditorUserId, -Amount, CurrencyCode from LedgerRecords where IsCanceled = 0
)
group by CreditorUserId, DebtorUserId, CurrencyCode");
    }

    public void Dispose() => _f.Dispose();

    private Guid AddBill(string description, DateTime date, decimal total, string currency, User creator, User[] people,
        ProcessingState status = ProcessingState.Draft)
    {
        var result = _f.BillService.Create(new BillCreationModel
        {
            CurrencyCode = currency,
            Description = description,
            Date = date,
            Lines =
            [
                new BillLineCreationModel
                {
                    ItemDescription = "Item",
                    Subtotal = total,
                    Participants = people.Select(p => new BillLineParticipantCreationModel { UserId = p.Id }).ToList(),
                },
            ],
            Payments = [new BillPaymentCreationModel { UserId = creator.Id, Amount = total }],
        }, creator.Id);
        Assert.Equal(BillActionStatus.Ok, result.Status);
        _f.Clear();

        // BillService.Create writes the participants itself (creator, line people and payers).
        var bill = _f.Db.Bills.Single(b => b.Id == result.Value);
        bill.Status = status;
        _f.Db.SaveChanges();

        _f.Clear();
        return result.Value;
    }

    /// <summary>A ledger record as finalize writes it: <paramref name="debtor"/> owes <paramref name="creditor"/>.</summary>
    private void AddLedger(Guid billId, User creditor, User debtor, decimal amount, string currency)
    {
        _f.Db.LedgerRecords.Add(new LedgerRecord
        {
            BillId = billId, CreditorUserId = creditor.Id, DebtorUserId = debtor.Id, Amount = amount, CurrencyCode = currency,
        });
        _f.Db.SaveChanges();
        _f.Clear();
    }

    private List<string> Descriptions(BillListQuery q) =>
        _f.BillRepository.GetForUser(_anna.Id, q).Items.Select(b => b.Description).ToList();

    private static DateTime Day(int d) => new(2026, 11, d, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Bills_DefaultIsNewestFirst_AndSortsByEachField()
    {
        AddBill("Pizza", Day(18), 50m, "EUR", _anna, [_anna, _boris]);
        AddBill("Cinema", Day(8), 24m, "EUR", _boris, [_anna, _boris], ProcessingState.Cancelled);
        AddBill("Groceries", Day(12), 63.4m, "PLN", _anna, [_anna, _clara], ProcessingState.Processed);

        Assert.Equal(["Pizza", "Groceries", "Cinema"], Descriptions(new BillListQuery()));
        Assert.Equal(["Cinema", "Groceries", "Pizza"], Descriptions(new BillListQuery { SortOrder = SortOrder.Asc }));
        Assert.Equal(["Cinema", "Groceries", "Pizza"], Descriptions(new BillListQuery { SortBy = BillSortField.Description, SortOrder = SortOrder.Asc }));
        Assert.Equal(["Groceries", "Pizza", "Cinema"], Descriptions(new BillListQuery { SortBy = BillSortField.Total }));
        Assert.Equal(["Pizza", "Groceries", "Cinema"], Descriptions(new BillListQuery { SortBy = BillSortField.Status, SortOrder = SortOrder.Asc }));
    }

    [Fact]
    public void Bills_Filters()
    {
        AddBill("Pizza night", Day(18), 50m, "EUR", _anna, [_anna, _boris]);
        AddBill("Cinema", Day(8), 24m, "EUR", _boris, [_anna, _boris], ProcessingState.Cancelled);
        AddBill("Groceries", Day(12), 63.4m, "PLN", _anna, [_anna, _clara], ProcessingState.Processed);
        AddBill("Not mine", Day(10), 10m, "EUR", _boris, [_boris, _clara]);

        Assert.Equal(["Pizza night", "Groceries"], Descriptions(new BillListQuery { CreatedByMe = true }));
        Assert.Equal(["Groceries"], Descriptions(new BillListQuery { CurrencyCode = "PLN" }));
        Assert.Equal(["Cinema"], Descriptions(new BillListQuery { Status = ProcessingState.Cancelled }));
        Assert.Equal(["Pizza night"], Descriptions(new BillListQuery { Search = "PIZZA" }));
        // Whole days: the 12th counts up to its end, and from the 12th starts at its beginning.
        Assert.Equal(["Groceries", "Cinema"], Descriptions(new BillListQuery { DateTo = new DateTime(2026, 11, 12) }));
        Assert.Equal(["Pizza night", "Groceries"], Descriptions(new BillListQuery { DateFrom = new DateTime(2026, 11, 12) }));
        Assert.Equal(["Groceries"], Descriptions(new BillListQuery { DateFrom = new DateTime(2026, 11, 12), DateTo = new DateTime(2026, 11, 12) }));
    }

    [Fact]
    public void Bills_WithUser_MatchesLedgerOrUnfinalizedParticipants()
    {
        AddBill("Draft with Boris", Day(20), 30m, "EUR", _anna, [_anna, _boris]);
        var taxi = AddBill("Taxi", Day(15), 30m, "EUR", _boris, [_anna, _boris, _clara], ProcessingState.Processed);
        AddLedger(taxi, _boris, _anna, 10m, "EUR");
        AddLedger(taxi, _boris, _clara, 10m, "EUR");
        var lunch = AddBill("Lunch with Clara", Day(14), 20m, "EUR", _clara, [_anna, _boris, _clara], ProcessingState.Processed);
        AddLedger(lunch, _clara, _anna, 7m, "EUR");

        // Web meaning: a ledger record between us, or a draft we're both in.
        Assert.Equal(["Draft with Boris", "Taxi"], Descriptions(new BillListQuery { WithUserId = _boris.Id }));
        // Boris is in "Lunch with Clara" but owes nothing to Anna, so it's not "with Boris".
        Assert.Equal(["Lunch with Clara"], Descriptions(new BillListQuery { WithUserId = _clara.Id }));
        // The bot keeps the ledger-only meaning.
        Assert.Equal(["Taxi"], _f.BillRepository.GetForUser(_anna.Id, _boris.Id, null).Items.Select(b => b.Description).ToList());
    }

    [Fact]
    public void Bills_PagingIsStableForEqualSortValues()
    {
        for (var i = 0; i < 5; i++)
            AddBill($"Same day {i}", Day(10), 10m, "EUR", _anna, [_anna, _boris]);

        var seen = new List<Guid>();
        for (var page = 0; page < 3; page++)
        {
            var result = _f.BillRepository.GetForUser(_anna.Id, new BillListQuery { PageNumber = page, CountPerPage = 2 });
            Assert.Equal(5, result.TotalCount);
            seen.AddRange(result.Items.Select(b => b.Id));
        }

        Assert.Equal(5, seen.Distinct().Count());
    }

    [Fact]
    public void Debts_OnlyOwn_NoSettled_FiltersAndSorts()
    {
        _f.UserContactService.EnsureContact(_anna.Id, _boris.Id, "Bob");
        _f.UserContactService.EnsureContact(_anna.Id, _clara.Id, "Clara");
        _f.UserContactService.EnsureContact(_boris.Id, _clara.Id, "Clara");
        _f.UserContactService.EnsureContact(_boris.Id, _anna.Id, "Anna");
        _f.UserContactService.EnsureContact(_clara.Id, _anna.Id, "Anna");
        _f.UserContactService.EnsureContact(_clara.Id, _boris.Id, "Boris");
        var bill = AddBill("Ledger holder", Day(1), 1m, "EUR", _anna, [_anna, _boris, _clara], ProcessingState.Processed);
        var bill2 = AddBill("Ledger holder 2", Day(2), 1m, "EUR", _boris, [_anna, _boris, _clara], ProcessingState.Processed);
        var bill3 = AddBill("Ledger holder 3", Day(3), 1m, "EUR", _clara, [_anna, _boris, _clara], ProcessingState.Processed);
        AddLedger(bill, _anna, _boris, 30m, "EUR");   // Bob owes Anna 30 EUR
        AddLedger(bill, _clara, _anna, 12.5m, "USD"); // Anna owes Clara 12.50 USD
        AddLedger(bill, _anna, _clara, 5m, "PLN");    // Clara owes Anna 5 PLN...
        AddLedger(bill3, _clara, _anna, 5m, "PLN");   // ...and Anna owes it back: settled
        AddLedger(bill2, _boris, _anna, 120m, "PLN"); // Anna owes Bob 120 PLN
        AddLedger(bill2, _clara, _boris, 7m, "USD");  // Boris owes Clara: not Anna's

        string Row(DebtModel d) => $"{d.DebtorUser.DisplayName} {d.Amount:0.##} {d.CurrencyCode}";
        List<string> Rows(DebtListQuery q) => _debts.GetOwn(_anna.Id, q).Items.Select(Row).ToList();

        Assert.Equal(["Bob 30 EUR", "Bob -120 PLN", "Clara -12.5 USD"], Rows(new DebtListQuery()));
        Assert.Equal(["Bob -120 PLN", "Clara -12.5 USD", "Bob 30 EUR"], Rows(new DebtListQuery { SortBy = DebtSortField.Amount }));
        Assert.Equal(["Bob 30 EUR", "Clara -12.5 USD", "Bob -120 PLN"], Rows(new DebtListQuery { SortBy = DebtSortField.Amount, SortOrder = SortOrder.Desc }));
        Assert.Equal(["Clara -12.5 USD", "Bob -120 PLN", "Bob 30 EUR"], Rows(new DebtListQuery { SortBy = DebtSortField.Currency, SortOrder = SortOrder.Desc }));
        Assert.Equal(["Bob 30 EUR"], Rows(new DebtListQuery { Direction = DebtDirection.OwedToMe }));
        Assert.Equal(["Bob -120 PLN", "Clara -12.5 USD"], Rows(new DebtListQuery { Direction = DebtDirection.IOwe }));
        Assert.Equal(["Bob 30 EUR", "Bob -120 PLN"], Rows(new DebtListQuery { WithUserId = _boris.Id }));
        Assert.Equal(["Bob -120 PLN"], Rows(new DebtListQuery { CurrencyCode = "PLN" }));

        var page = _debts.GetOwn(_anna.Id, new DebtListQuery { CountPerPage = 2, PageNumber = 1 });
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(["Clara -12.5 USD"], page.Items.Select(Row).ToList());
    }
}
