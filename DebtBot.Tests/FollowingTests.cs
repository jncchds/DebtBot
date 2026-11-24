using System.Security.Claims;
using DebtBot.Controllers;
using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;
using DebtBot.Models.Debt;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
using DebtBot.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace DebtBot.Tests;

/// <summary>
/// T7b: <c>ofUserId</c> on Debts, Bills and Spendings (confirmed subscription or yourself, else 403), bill read access for
/// confirmed subscribers of a participant, and changes staying creator-or-admin.
/// Anna is subscribed (confirmed) to Boris, pending to Clara, and has nothing with Dmitry.
/// </summary>
public sealed class FollowingTests : IDisposable
{
    private readonly SqliteFixture _f = new();
    private readonly User _anna;
    private readonly User _boris;
    private readonly User _clara;
    private readonly User _dmitry;
    private readonly User _grisha;

    public FollowingTests()
    {
        _anna = _f.AddUser("Anna");
        _boris = _f.AddUser("Boris");
        _clara = _f.AddUser("Clara");
        _dmitry = _f.AddUser("Dmitry");
        _grisha = _f.AddUser("Grisha", telegramUserName: "@grisha_v");

        // EnsureCreated makes no views; this mirrors the Postgres "debts" materialized view (as in ListQueryTests).
        _f.Db.Database.ExecuteSqlRaw(@"
create view debts as
select CreditorUserId, DebtorUserId, CurrencyCode, sum(Amount) as Amount
from (
    select CreditorUserId, DebtorUserId, Amount, CurrencyCode from LedgerRecords where IsCanceled = 0
    union all
    select DebtorUserId, CreditorUserId, -Amount, CurrencyCode from LedgerRecords where IsCanceled = 0
)
group by CreditorUserId, DebtorUserId, CurrencyCode");

        Subscribe(_anna, _boris, confirmed: true);
        Subscribe(_anna, _clara, confirmed: false);
        // Someone else's subscription to Dmitry doesn't count for Anna.
        Subscribe(_boris, _dmitry, confirmed: true);
    }

    public void Dispose() => _f.Dispose();

    private void Subscribe(User subscriber, User user, bool confirmed)
    {
        _f.Db.NotificationSubscriptions.Add(new NotificationSubscription { SubscriberId = subscriber.Id, UserId = user.Id, IsConfirmed = confirmed });
        _f.Db.SaveChanges();
        _f.Clear();
    }

    private static DateTime Day(int d) => new(2026, 11, d, 12, 0, 0, DateTimeKind.Utc);

    private Guid AddBill(string description, DateTime date, decimal total, User creator, User[] people, ProcessingState status = ProcessingState.Processed)
    {
        var result = _f.BillService.Create(new BillCreationModel
        {
            CurrencyCode = "EUR",
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

        // The EnsureBillParticipant consumer isn't running here: add the participants it would.
        foreach (var userId in people.Select(p => p.Id).Append(creator.Id).Distinct())
            _f.Db.BillParticipants.Add(new BillParticipant { BillId = result.Value, UserId = userId });

        var bill = _f.Db.Bills.Single(b => b.Id == result.Value);
        bill.Status = status;
        _f.Db.SaveChanges();
        _f.Clear();
        return result.Value;
    }

    /// <summary><paramref name="debtor"/> owes <paramref name="creditor"/> on the bill.</summary>
    private void AddLedger(Guid billId, User creditor, User debtor, decimal amount)
    {
        _f.Db.LedgerRecords.Add(new LedgerRecord { BillId = billId, CreditorUserId = creditor.Id, DebtorUserId = debtor.Id, Amount = amount, CurrencyCode = "EUR" });
        _f.Db.SaveChanges();
        _f.Clear();
    }

    private void AddSpending(User user, Guid billId, decimal amount)
    {
        _f.Db.Spendings.Add(new Spending
        {
            BillId = billId, UserId = user.Id, Description = "Spending", Amount = amount, CurrencyCode = "EUR",
            PaymentAmount = amount, PaymentCurrencyCode = "EUR", Portion = 0.5m, Date = Day(10),
        });
        _f.Db.SaveChanges();
        _f.Clear();
    }

    private static T As<T>(T controller, User me, bool admin = false) where T : ControllerBase
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, me.Id.ToString()) };
        if (admin)
            claims.Add(new Claim(DebtBot.Identity.IdentityData.AdminUserClaimName, "true"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) },
        };
        return controller;
    }

    private DebtsController Debts(User me, bool admin = false) =>
        As(new DebtsController(new DebtService(_f.Db, _f.Mapper), _f.UserContactService), me, admin);

    private SpendingsController Spendings(User me) =>
        As(new SpendingsController(new BudgetService(_f.Db, _f.Mapper), _f.UserContactService), me);

    private BillsController Bills(User me, bool admin = false) =>
        As(new BillsController(_f.BillService, Substitute.For<IExcelService>(), _f.UserService, Substitute.For<IParserService>(), _f.UserContactService), me, admin);

    private static int? Status(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode;
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    [Fact]
    public void Debts_OfConfirmed_AreTheirBalancesUnderOwnNames()
    {
        // Boris calls Anna "Annushka" and Grisha "G": the subscriber sees their own names instead.
        _f.UserContactService.EnsureContact(_boris.Id, _anna.Id, "Annushka");
        _f.UserContactService.EnsureContact(_boris.Id, _grisha.Id, "G");
        _f.UserContactService.EnsureContact(_anna.Id, _boris.Id, "Bob");
        _f.UserContactService.EnsureContact(_grisha.Id, _boris.Id, "Boris");
        var bill = AddBill("Ledger holder", Day(1), 1m, _boris, [_anna, _boris, _grisha]);
        AddLedger(bill, _boris, _anna, 30m);   // Anna owes Boris 30
        AddLedger(bill, _grisha, _boris, 18.4m); // Boris owes Grisha 18.40

        var page = Value<PagingResult<DebtModel>>(Debts(_anna).GetOwn(new DebtListQuery { OfUserId = _boris.Id }));
        Assert.Equal(["Anna 30", "Grisha -18.4"], page.Items.Select(d => $"{d.DebtorUser.DisplayName} {d.Amount:0.##}"));
        Assert.All(page.Items, d => Assert.Equal(_boris.Id, d.CreditorUser.Id));
        Assert.Equal("@grisha_v", page.Items[1].DebtorUser.TelegramUserName);

        // Filters and sort work from Boris's side.
        var owes = Value<PagingResult<DebtModel>>(Debts(_anna).GetOwn(new DebtListQuery { OfUserId = _boris.Id, Direction = DebtDirection.IOwe }));
        Assert.Equal([_grisha.Id], owes.Items.Select(d => d.DebtorUser.Id));
        var desc = Value<PagingResult<DebtModel>>(Debts(_anna).GetOwn(new DebtListQuery { OfUserId = _boris.Id, SortOrder = SortOrder.Desc }));
        Assert.Equal(["Grisha", "Anna"], desc.Items.Select(d => d.DebtorUser.DisplayName));

        // Without ofUserId (or with yourself) nothing changes: your own balances under your names.
        var mine = Value<PagingResult<DebtModel>>(Debts(_anna).GetOwn(new DebtListQuery()));
        Assert.Equal(["Bob -30"], mine.Items.Select(d => $"{d.DebtorUser.DisplayName} {d.Amount:0.##}"));
        var self = Value<PagingResult<DebtModel>>(Debts(_anna).GetOwn(new DebtListQuery { OfUserId = _anna.Id }));
        Assert.Equal(["Bob -30"], self.Items.Select(d => $"{d.DebtorUser.DisplayName} {d.Amount:0.##}"));
    }

    [Fact]
    public void Lists_OfPendingOrNone_Are403_EvenForAdmins()
    {
        foreach (var other in new[] { _clara, _dmitry })
        {
            Assert.Equal(403, Status(Debts(_anna).GetOwn(new DebtListQuery { OfUserId = other.Id })));
            Assert.Equal(403, Status(Debts(_anna, admin: true).GetOwn(new DebtListQuery { OfUserId = other.Id })));
            Assert.Equal(403, Status(Bills(_anna).Get(new BillListQuery { OfUserId = other.Id })));
            Assert.Equal(403, Status(Spendings(_anna).GetOwn(new SpendingListQuery { OfUserId = other.Id })));
            Assert.Equal(403, Status(Spendings(_anna).GetOwnSummary(ofUserId: other.Id)));
        }

        // The other direction doesn't count: Boris has no subscription to Anna.
        Assert.Equal(403, Status(Bills(_boris).Get(new BillListQuery { OfUserId = _anna.Id })));
    }

    [Fact]
    public void Bills_OfConfirmed_AreTheBillsTheyTakePartIn()
    {
        var withAnna = AddBill("Pizza night", Day(18), 50m, _anna, [_anna, _boris]);
        var withGrisha = AddBill("Board games", Day(13), 36.8m, _grisha, [_boris, _grisha]);
        AddBill("Not Boris's", Day(12), 10m, _anna, [_anna, _clara]);
        var groceries = AddBill("Groceries", Day(9), 42m, _boris, [_boris], ProcessingState.Draft);

        var page = Value<PagingResult<BillListModel>>(Bills(_anna).Get(new BillListQuery { OfUserId = _boris.Id }));
        Assert.Equal([withAnna, withGrisha, groceries], page.Items.Select(b => b.Id));
        Assert.Contains(_anna.Id, page.Items[0].ParticipantIds);
        Assert.DoesNotContain(_anna.Id, page.Items[1].ParticipantIds);

        // createdByMe and withUserId are meant from Boris's side.
        var created = Value<PagingResult<BillListModel>>(Bills(_anna).Get(new BillListQuery { OfUserId = _boris.Id, CreatedByMe = true }));
        Assert.Equal([groceries], created.Items.Select(b => b.Id));
        var withG = Value<PagingResult<BillListModel>>(Bills(_anna).Get(new BillListQuery { OfUserId = _boris.Id, WithUserId = _grisha.Id }));
        Assert.Equal([withGrisha], withG.Items.Select(b => b.Id));

        var self = Value<PagingResult<BillListModel>>(Bills(_anna).Get(new BillListQuery { OfUserId = _anna.Id }));
        Assert.Equal(2, self.TotalCount);
    }

    [Fact]
    public void Spendings_OfConfirmed_AreTheirs()
    {
        var bill = AddBill("Taxi", Day(15), 30m, _boris, [_anna, _boris]);
        AddSpending(_boris, bill, 20m);
        AddSpending(_anna, bill, 10m);

        var page = Value<PagingResult<SpendingModel>>(Spendings(_anna).GetOwn(new SpendingListQuery { OfUserId = _boris.Id }));
        Assert.Equal([20m], page.Items.Select(s => s.Amount));
        var summary = Value<List<SpendingSummaryModel>>(Spendings(_anna).GetOwnSummary(ofUserId: _boris.Id));
        Assert.Equal([("EUR", 20m, 1)], summary.Select(s => (s.CurrencyCode, s.Amount, s.Count)));

        var mine = Value<PagingResult<SpendingModel>>(Spendings(_anna).GetOwn(new SpendingListQuery { OfUserId = _anna.Id }));
        Assert.Equal([10m], mine.Items.Select(s => s.Amount));
        Assert.Equal(10m, Value<List<SpendingSummaryModel>>(Spendings(_anna).GetOwnSummary()).Single().Amount);
    }

    [Fact]
    public async Task BillRead_ForConfirmedSubscriberOfAnyParticipant()
    {
        var bill = AddBill("Board games", Day(13), 36.8m, _grisha, [_boris, _grisha]);
        var lineId = _f.Db.BillLines.Single(l => l.BillId == bill).Id;

        Assert.Equal(bill, Value<BillModel>(await Bills(_anna).GetAsync(bill, default)).Id);
        Assert.Equal(lineId, Value<BillLineModel>(await Bills(_anna).GetLineAsync(lineId, default)).Id);

        // Pending (Clara) or nothing (Dmitry) on the bill gives nothing to Anna; Boris's subscription to Dmitry is his.
        var claras = AddBill("Clara's", Day(14), 10m, _clara, [_clara, _dmitry]);
        Assert.Equal(404, Status(await Bills(_anna).GetAsync(claras, default)));
        var claraLine = _f.Db.BillLines.Single(l => l.BillId == claras).Id;
        Assert.Equal(404, Status(await Bills(_anna).GetLineAsync(claraLine, default)));
        Assert.Equal(200, Status(await Bills(_boris).GetAsync(claras, default)));
    }

    [Fact]
    public async Task BillRead_ViaParticipantsListOnly()
    {
        // A bill whose participants list has Boris before he's on a line or payment (as the list shows it).
        var bill = AddBill("Dinner", Day(13), 20m, _grisha, [_grisha], ProcessingState.Draft);
        _f.Db.BillParticipants.Add(new BillParticipant { BillId = bill, UserId = _boris.Id });
        _f.Db.SaveChanges();
        _f.Clear();

        Assert.Equal(200, Status(await Bills(_anna).GetAsync(bill, default)));
    }

    [Fact]
    public async Task Subscriber_CannotChangeTheBill()
    {
        var draft = AddBill("Groceries", Day(9), 42m, _boris, [_boris, _grisha], ProcessingState.Draft);
        var line = new BillLineCreationModel
        {
            ItemDescription = "Milk", Subtotal = 2m,
            Participants = [new BillLineParticipantCreationModel { UserId = _boris.Id }],
        };

        var bills = Bills(_anna);
        Assert.Equal(404, Status(await bills.FinalizeAsync(draft, default)));
        Assert.Equal(404, Status(await bills.CancelAsync(draft, default)));
        Assert.Equal(404, Status(await bills.AddLinesAsync(draft, [line], default)));
        Assert.Equal(404, Status(await bills.AddPaymentsAsync(draft, [new BillPaymentCreationModel { UserId = _boris.Id, Amount = 2m }], default)));

        var pair = _f.BillService.CreateExchange(new ExchangeCreationModel
        {
            FromUserId = _boris.Id, ToUserId = _grisha.Id, FromCurrencyCode = "EUR", ToCurrencyCode = "PLN", FromAmount = 10m, ToAmount = 43m,
        }, _boris.Id);
        Assert.Equal(BillActionStatus.Ok, pair.Status);
        _f.Clear();
        Assert.Equal(404, Status(await bills.FinalizeExchangeAsync(pair.Value!, default)));

        // Nothing changed.
        Assert.Equal(ProcessingState.Draft, _f.Db.Bills.Single(b => b.Id == draft).Status);
        Assert.Single(_f.Db.BillLines.Where(l => l.BillId == draft));
        Assert.Single(_f.Db.BillPayments.Where(p => p.BillId == draft));
        Assert.All(_f.Db.Bills.Where(b => b.Id == pair.Value!.ForwardBillId || b.Id == pair.Value!.BackwardBillId), b => Assert.Equal(ProcessingState.Draft, b.Status));
    }
}
