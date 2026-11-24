using DebtBot.Consumers;
using DebtBot.DB.Entities;
using DebtBot.Messages;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;
using DebtBot.Models;
using DebtBot.Models.Enums;
using DebtBot.Services;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DebtBot.Tests;

/// <summary>
/// Series 2, T1: a creator who is on no line and no payment still owns the bill, and PUT Bills/{id} replaces a
/// draft (the draft page saves the whole New bill form at once).
/// </summary>
public sealed class CreatorLeaveTests : IDisposable
{
    private readonly SqliteFixture _f = new();
    private readonly User _anna;
    private readonly User _boris;
    private readonly User _dmitry;
    private readonly User _stranger;
    private readonly User _admin;

    public CreatorLeaveTests()
    {
        _anna = _f.AddUser("Anna");
        _boris = _f.AddUser("Boris");
        _dmitry = _f.AddUser("Dmitry");
        _stranger = _f.AddUser("Stranger");
        _admin = _f.AddUser("Admin", role: UserRole.Admin);
    }

    public void Dispose() => _f.Dispose();

    private static BillLineCreationModel Line(string item, decimal subtotal, params (Guid id, decimal? part)[] people) => new()
    {
        ItemDescription = item,
        Subtotal = subtotal,
        Participants = people.Select(p => new BillLineParticipantCreationModel { UserId = p.id, Part = p.part }).ToList(),
    };

    /// <summary>Anna's bill where only Boris and Dmitry take part: Boris pays 60, both share 60 equally.</summary>
    private BillCreationModel WithoutCreator() => new()
    {
        CurrencyCode = "EUR",
        Description = "Dinner",
        Lines = [Line("Pasta", 60m, (_boris.Id, null), (_dmitry.Id, null))],
        Payments = [new BillPaymentCreationModel { UserId = _boris.Id, Amount = 60m }],
    };

    private Guid Create(BillCreationModel model, User creator)
    {
        var result = _f.BillService.Create(model, creator.Id);
        Assert.Equal(BillActionStatus.Ok, result.Status);
        _f.Clear();
        return result.Value;
    }

    private async Task RunFinalizeConsumer(Guid billId)
    {
        var context = Substitute.For<ConsumeContext<BillFinalized>>();
        context.Message.Returns(new BillFinalized(billId, false));
        await new BillFinalizedConsumer(_f.Db, _f.Publish, NullLogger<BillFinalizedConsumer>.Instance).Consume(context);
        _f.Clear();
    }

    private List<Guid> Participants(Guid billId) =>
        _f.Db.BillParticipants.Where(p => p.BillId == billId).Select(p => p.UserId).ToList();

    private static BillListQuery Mine() => new();

    [Fact]
    public async Task CreatorOnNoLineOrPayment_OwnsTheBill_AndGetsNoDebts()
    {
        var id = Create(WithoutCreator(), _anna);

        // Still a participant (ownership and lists), and the bill is in Anna's list.
        Assert.Contains(_anna.Id, Participants(id));
        Assert.Contains(_f.BillRepository.GetForUser(_anna.Id, Mine()).Items, b => b.Id == id);

        // Anna can still change it.
        var edited = await _f.BillService.AddLinesAsync(id, [Line("Bread", 4m, (_dmitry.Id, null))], _anna.Id, false, default);
        Assert.Equal(BillActionStatus.Ok, edited.Status);

        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.FinalizeAsync(id, _anna.Id, false, default)).Status);
        await RunFinalizeConsumer(id);

        var bill = _f.Db.Bills.Single(b => b.Id == id);
        Assert.Equal(ProcessingState.Processed, bill.Status);
        var records = _f.Db.LedgerRecords.Where(r => r.BillId == id).ToList();
        var record = Assert.Single(records);
        Assert.Equal(_boris.Id, record.CreditorUserId);
        Assert.Equal(_dmitry.Id, record.DebtorUserId);
        Assert.DoesNotContain(records, r => r.CreditorUserId == _anna.Id || r.DebtorUserId == _anna.Id);
        Assert.DoesNotContain(_f.Db.Spendings.Where(s => s.BillId == id), s => s.UserId == _anna.Id);

        // Still listed after finalizing, and Anna can cancel it.
        Assert.Contains(_f.BillRepository.GetForUser(_anna.Id, Mine()).Items, b => b.Id == id);
        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.CancelAsync(id, _anna.Id, false, default)).Status);
    }

    [Fact]
    public async Task ReplaceDraft_ReplacesEverything_AndRebuildsParticipants()
    {
        // Anna, Boris and Dmitry on the bill; Anna paid.
        var id = Create(new BillCreationModel
        {
            CurrencyCode = "EUR",
            Description = "Dinner",
            Lines = [Line("Pasta", 30m, (_anna.Id, null), (_boris.Id, null), (_dmitry.Id, null))],
            Payments = [new BillPaymentCreationModel { UserId = _anna.Id, Amount = 30m }],
        }, _anna);
        var date = _f.Db.Bills.Single(b => b.Id == id).Date;

        // Anna leaves, Dmitry is dropped, Boris pays; Tiramisu split by parts.
        var model = new BillCreationModel
        {
            CurrencyCode = "pln",
            Description = "Dinner\nat Molto",
            Lines = [Line("Pasta", 40m, (_boris.Id, null)), Line("Tiramisu", 10m, (_boris.Id, 1m), (_admin.Id, 2m))],
            Payments = [new BillPaymentCreationModel { UserId = _boris.Id, Amount = 50m }],
        };
        var result = await _f.BillService.ReplaceDraftAsync(id, model, _anna.Id, false, default);

        Assert.Equal(BillActionStatus.Ok, result.Status);
        var bill = result.Value!;
        Assert.Equal("PLN", bill.CurrencyCode);
        Assert.Equal("PLN", bill.PaymentCurrencyCode);
        Assert.Equal("Dinner\nat Molto", bill.Description);
        Assert.Equal(50m, bill.TotalWithTips);
        Assert.Equal(date, bill.Date);
        Assert.Equal(["Pasta", "Tiramisu"], bill.Lines.Select(l => l.ItemDescription).OrderBy(x => x).ToList());
        Assert.Equal(2m, bill.Lines.Single(l => l.ItemDescription == "Tiramisu").Participants.Single(p => p.User.Id == _admin.Id).Part);
        var payment = Assert.Single(bill.Payments);
        Assert.Equal(_boris.Id, payment.User.Id);

        _f.Clear();
        Assert.Equal(3, _f.Db.BillLineParticipants.Count(p => p.BillLine.BillId == id));
        Assert.Equal(new[] { _anna.Id, _boris.Id, _admin.Id }.OrderBy(x => x), Participants(id).OrderBy(x => x));
        Assert.Equal(_anna.Id, _f.Db.Bills.Single(b => b.Id == id).CreatorId);
    }

    [Fact]
    public async Task ReplaceDraft_ChecksAccess_AndAnAdminKeepsTheCreator()
    {
        var id = Create(WithoutCreator(), _anna);

        Assert.Equal(BillActionStatus.NotFound, (await _f.BillService.ReplaceDraftAsync(id, WithoutCreator(), _stranger.Id, false, default)).Status);
        Assert.Equal(BillActionStatus.Forbidden, (await _f.BillService.ReplaceDraftAsync(id, WithoutCreator(), _boris.Id, false, default)).Status);
        Assert.Equal(BillActionStatus.NotFound, (await _f.BillService.ReplaceDraftAsync(Guid.NewGuid(), WithoutCreator(), _anna.Id, false, default)).Status);

        var model = WithoutCreator();
        model.Lines![0].Participants!.RemoveAt(1);
        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.ReplaceDraftAsync(id, model, _admin.Id, true, default)).Status);

        _f.Clear();
        Assert.Equal(_anna.Id, _f.Db.Bills.Single(b => b.Id == id).CreatorId);
        Assert.Equal(new[] { _anna.Id, _boris.Id }.OrderBy(x => x), Participants(id).OrderBy(x => x));
    }

    [Fact]
    public async Task ReplaceDraft_ValidatesLikeCreate()
    {
        var id = Create(WithoutCreator(), _anna);

        var badCurrency = WithoutCreator();
        badCurrency.CurrencyCode = "EU";
        Assert.Equal(BillActionStatus.Invalid, (await _f.BillService.ReplaceDraftAsync(id, badCurrency, _anna.Id, false, default)).Status);

        var unknown = WithoutCreator();
        unknown.Payments = [new BillPaymentCreationModel { UserId = Guid.NewGuid(), Amount = 5m }];
        Assert.Equal(BillActionStatus.Invalid, (await _f.BillService.ReplaceDraftAsync(id, unknown, _anna.Id, false, default)).Status);

        var emptyLine = WithoutCreator();
        emptyLine.Lines = [Line("Nobody", 5m)];
        Assert.Equal(BillActionStatus.Invalid, (await _f.BillService.ReplaceDraftAsync(id, emptyLine, _anna.Id, false, default)).Status);

        // Nothing changed.
        _f.Clear();
        Assert.Equal("EUR", _f.Db.Bills.Single(b => b.Id == id).CurrencyCode);
        Assert.Equal(60m, _f.Db.BillPayments.Single(p => p.BillId == id).Amount);
    }

    [Fact]
    public async Task ReplaceDraft_NotADraftIsAConflict()
    {
        var id = Create(WithoutCreator(), _anna);
        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.FinalizeAsync(id, _anna.Id, false, default)).Status);
        _f.Clear();

        var result = await _f.BillService.ReplaceDraftAsync(id, WithoutCreator(), _anna.Id, false, default);
        Assert.Equal(BillActionStatus.Conflict, result.Status);
    }

    [Fact]
    public void ReplaceDraft_InTheRepository_ChecksTheStatusInsideTheTransaction()
    {
        // The service saw a draft, but it was finalized before the replace ran: the repository refuses and changes nothing.
        var id = Create(WithoutCreator(), _anna);
        _f.Db.Bills.Single(b => b.Id == id).Status = ProcessingState.Ready;
        _f.Db.SaveChanges();
        _f.Clear();

        var parsed = BillRules.ToParserModel(new BillCreationModel
        {
            CurrencyCode = "USD",
            Lines = [Line("Other", 1m, (_anna.Id, null))],
            Payments = [new BillPaymentCreationModel { UserId = _anna.Id, Amount = 1m }],
        }, DateTime.UtcNow);
        Assert.False(_f.BillRepository.ReplaceDraft(id, parsed, _f.UserService.GetUserById(_anna.Id)!));

        _f.Clear();
        Assert.Equal("EUR", _f.Db.Bills.Single(b => b.Id == id).CurrencyCode);
        Assert.Equal("Pasta", _f.Db.BillLines.Single(l => l.BillId == id).ItemDescription);
    }

    [Fact]
    public async Task ReplaceDraft_RefusesAnExchange()
    {
        var pair = _f.BillService.CreateExchange(new DebtBot.Models.Exchange.ExchangeCreationModel
        {
            FromUserId = _anna.Id,
            ToUserId = _boris.Id,
            FromAmount = 10m,
            FromCurrencyCode = "EUR",
            ToAmount = 40m,
            ToCurrencyCode = "PLN",
        }, _anna.Id);
        Assert.Equal(BillActionStatus.Ok, pair.Status);
        _f.Clear();

        var result = await _f.BillService.ReplaceDraftAsync(pair.Value!.ForwardBillId, WithoutCreator(), _anna.Id, false, default);
        Assert.Equal(BillActionStatus.Invalid, result.Status);
    }
}
