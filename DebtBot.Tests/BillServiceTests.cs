using DebtBot.DB.Entities;
using DebtBot.Messages;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
using DebtBot.Services;
using NSubstitute;

namespace DebtBot.Tests;

/// <summary>The structured bill operations and their access checks, on the real repositories over SQLite.</summary>
public sealed class BillServiceTests : IDisposable
{
    private readonly SqliteFixture _f = new();
    private readonly User _anna;
    private readonly User _boris;
    private readonly User _stranger;
    private readonly User _admin;

    public BillServiceTests()
    {
        _anna = _f.AddUser("Anna");
        _boris = _f.AddUser("Boris");
        _stranger = _f.AddUser("Stranger");
        _admin = _f.AddUser("Admin", role: UserRole.Admin);
    }

    public void Dispose() => _f.Dispose();

    private Guid CreateDraft()
    {
        var result = _f.BillService.Create(new BillCreationModel
        {
            CurrencyCode = "EUR",
            Description = "Dinner",
            Lines =
            [
                new BillLineCreationModel
                {
                    ItemDescription = "Pizza",
                    Subtotal = 30m,
                    Participants =
                    [
                        new BillLineParticipantCreationModel { UserId = _anna.Id },
                        new BillLineParticipantCreationModel { UserId = _boris.Id },
                    ],
                },
            ],
            Payments = [new BillPaymentCreationModel { UserId = _anna.Id, Amount = 30m }],
        }, _anna.Id);

        Assert.Equal(BillActionStatus.Ok, result.Status);
        _f.Clear();
        return result.Value;
    }

    private Bill LoadBill(Guid id)
    {
        _f.Clear();
        return _f.Db.Bills.Single(b => b.Id == id);
    }

    [Fact]
    public void Create_MakesADraftWithEqualSplit()
    {
        var id = CreateDraft();

        var bill = LoadBill(id);
        Assert.Equal(ProcessingState.Draft, bill.Status);
        Assert.Equal(30m, bill.TotalWithTips);
        Assert.Equal(_anna.Id, bill.CreatorId);
        var line = _f.Db.BillLines.Single(l => l.BillId == id);
        Assert.Equal([1m, 1m], _f.Db.BillLineParticipants.Where(p => p.BillLineId == line.Id).Select(p => p.Part).ToList());
        Assert.Equal(30m, _f.Db.BillPayments.Single(p => p.BillId == id && p.UserId == _anna.Id).Amount);
    }

    [Fact]
    public void Create_RejectsUnknownUsers()
    {
        var result = _f.BillService.Create(new BillCreationModel
        {
            CurrencyCode = "EUR",
            Payments = [new BillPaymentCreationModel { UserId = Guid.NewGuid(), Amount = 5m }],
        }, _anna.Id);

        Assert.Equal(BillActionStatus.Invalid, result.Status);
        Assert.Empty(_f.Db.Bills);
    }

    [Fact]
    public async Task AddLines_SplitByParts_AndTotalFollowsLines()
    {
        var id = CreateDraft();

        var result = await _f.BillService.AddLinesAsync(id,
        [
            new BillLineCreationModel
            {
                ItemDescription = "Wine",
                Subtotal = 20m,
                Participants =
                [
                    new BillLineParticipantCreationModel { UserId = _anna.Id, Part = 3m },
                    new BillLineParticipantCreationModel { UserId = _boris.Id, Part = 1m },
                ],
            },
        ], _anna.Id, false, default);

        Assert.Equal(BillActionStatus.Ok, result.Status);
        Assert.Equal(50m, result.Value!.TotalWithTips);
        var wine = Assert.Single(result.Value.Lines, l => l.ItemDescription == "Wine");
        Assert.Equal(3m, wine.Participants.Single(p => p.User.Id == _anna.Id).Part);
        Assert.Equal(1m, wine.Participants.Single(p => p.User.Id == _boris.Id).Part);
        await _f.Publish.Received().Publish(Arg.Is<EnsureBillParticipant>(m => m.BillId == id && m.UserId == _boris.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddPayments_AddsToTheDraft()
    {
        var id = CreateDraft();

        var result = await _f.BillService.AddPaymentsAsync(id,
            [new BillPaymentCreationModel { UserId = _boris.Id, Amount = 10m }], _anna.Id, false, default);

        Assert.Equal(BillActionStatus.Ok, result.Status);
        Assert.Equal(2, result.Value!.Payments.Count);
    }

    [Fact]
    public async Task AddLinesAndPayments_OnlyOnDrafts()
    {
        var id = CreateDraft();
        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.FinalizeAsync(id, _anna.Id, false, default)).Status);

        var lines = await _f.BillService.AddLinesAsync(id,
            [new BillLineCreationModel { ItemDescription = "x", Subtotal = 1m, Participants = [new() { UserId = _anna.Id }] }],
            _anna.Id, false, default);
        var payments = await _f.BillService.AddPaymentsAsync(id,
            [new BillPaymentCreationModel { UserId = _anna.Id, Amount = 1m }], _anna.Id, false, default);

        Assert.Equal(BillActionStatus.Invalid, lines.Status);
        Assert.Equal(BillActionStatus.Invalid, payments.Status);
    }

    [Fact]
    public async Task Finalize_NonParticipantGetsNotFound_ParticipantGetsForbidden()
    {
        var id = CreateDraft();

        Assert.Equal(BillActionStatus.NotFound, (await _f.BillService.FinalizeAsync(id, _stranger.Id, false, default)).Status);
        Assert.Equal(BillActionStatus.Forbidden, (await _f.BillService.FinalizeAsync(id, _boris.Id, false, default)).Status);
        Assert.Equal(BillActionStatus.NotFound, (await _f.BillService.FinalizeAsync(Guid.NewGuid(), _anna.Id, false, default)).Status);
        Assert.Equal(ProcessingState.Draft, LoadBill(id).Status);
        await _f.Publish.DidNotReceive().Publish(Arg.Any<BillFinalized>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finalize_CreatorAndAdminCan()
    {
        var mine = CreateDraft();
        var other = CreateDraft();

        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.FinalizeAsync(mine, _anna.Id, false, default)).Status);
        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.FinalizeAsync(other, _admin.Id, true, default)).Status);

        Assert.Equal(ProcessingState.Ready, LoadBill(mine).Status);
        Assert.Equal(ProcessingState.Ready, LoadBill(other).Status);
        await _f.Publish.Received(2).Publish(Arg.Any<BillFinalized>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finalize_RejectsAnIncompleteDraft()
    {
        var result = _f.BillService.Create(new BillCreationModel { CurrencyCode = "EUR" }, _anna.Id);

        var finalize = await _f.BillService.FinalizeAsync(result.Value, _anna.Id, false, default);

        Assert.Equal(BillActionStatus.Invalid, finalize.Status);
        Assert.Equal(ProcessingState.Draft, LoadBill(result.Value).Status);
    }

    [Fact]
    public async Task Cancel_NonParticipantGetsNotFound_ParticipantGetsForbidden_CreatorCan()
    {
        var id = CreateDraft();

        Assert.Equal(BillActionStatus.NotFound, (await _f.BillService.CancelAsync(id, _stranger.Id, false, default)).Status);
        Assert.Equal(BillActionStatus.Forbidden, (await _f.BillService.CancelAsync(id, _boris.Id, false, default)).Status);
        await _f.Publish.DidNotReceive().Publish(Arg.Any<BillCanceled>(), Arg.Any<CancellationToken>());

        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.CancelAsync(id, _anna.Id, false, default)).Status);
        await _f.Publish.Received(1).Publish(Arg.Is<BillCanceled>(m => m.BillId == id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledIsInvalid()
    {
        var id = CreateDraft();
        var bill = _f.Db.Bills.Single(b => b.Id == id);
        bill.Status = ProcessingState.Cancelled;
        _f.Db.SaveChanges();

        Assert.Equal(BillActionStatus.Invalid, (await _f.BillService.CancelAsync(id, _admin.Id, true, default)).Status);
    }

    [Fact]
    public async Task GetLine_ChecksAccess()
    {
        var id = CreateDraft();
        var lineId = _f.Db.BillLines.Single(l => l.BillId == id).Id;

        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.GetLineAsync(lineId, _boris.Id, false, default)).Status);
        Assert.Equal(BillActionStatus.NotFound, (await _f.BillService.GetLineAsync(lineId, _stranger.Id, false, default)).Status);
        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.GetLineAsync(lineId, _admin.Id, true, default)).Status);
    }

    private ExchangeCreationModel Exchange() => new()
    {
        FromUserId = _anna.Id,
        FromAmount = 100m,
        FromCurrencyCode = "EUR",
        ToUserId = _boris.Id,
        ToAmount = 430m,
        ToCurrencyCode = "PLN",
    };

    [Fact]
    public async Task CreateExchange_MakesTwoLinkedDrafts()
    {
        var result = _f.BillService.CreateExchange(Exchange(), _anna.Id);

        Assert.Equal(BillActionStatus.Ok, result.Status);
        var forward = await _f.BillService.GetAsync(result.Value!.ForwardBillId, default);
        var backward = await _f.BillService.GetAsync(result.Value.BackwardBillId, default);

        Assert.Equal(ProcessingState.Draft, forward!.Status);
        Assert.Equal(ProcessingState.Draft, backward!.Status);
        Assert.Equal(backward.Id, forward.ExchangeBillId);
        Assert.Equal(forward.Id, backward.ExchangeBillId);

        Assert.Equal(100m, Assert.Single(forward.Payments).Amount);
        Assert.Equal(_anna.Id, forward.Payments[0].User.Id);
        Assert.Equal("EUR", forward.PaymentCurrencyCode);
        Assert.Equal(430m, Assert.Single(backward.Payments).Amount);
        Assert.Equal(_boris.Id, backward.Payments[0].User.Id);
        Assert.Equal("PLN", backward.PaymentCurrencyCode);
    }

    [Theory]
    [InlineData(0, 430)]
    [InlineData(100, -1)]
    public void CreateExchange_RejectsAmountsNotAboveZero(decimal from, decimal to)
    {
        var exchange = Exchange();
        exchange.FromAmount = from;
        exchange.ToAmount = to;

        Assert.Equal(BillActionStatus.Invalid, _f.BillService.CreateExchange(exchange, _anna.Id).Status);
        Assert.Empty(_f.Db.Bills);
    }

    [Fact]
    public void CreateExchange_RejectsTheSameUserTwice()
    {
        var exchange = Exchange();
        exchange.ToUserId = _anna.Id;

        Assert.Equal(BillActionStatus.Invalid, _f.BillService.CreateExchange(exchange, _anna.Id).Status);
        Assert.Empty(_f.Db.Bills);
    }

    [Fact]
    public void CreateExchange_RejectsUnknownUsers()
    {
        var exchange = Exchange();
        exchange.ToUserId = Guid.NewGuid();

        Assert.Equal(BillActionStatus.Invalid, _f.BillService.CreateExchange(exchange, _anna.Id).Status);
    }

    [Fact]
    public async Task FinalizeExchange_FinalizesBothBills()
    {
        var pair = _f.BillService.CreateExchange(Exchange(), _anna.Id).Value!;
        _f.Clear();

        var result = await _f.BillService.FinalizeExchangeAsync(pair.ForwardBillId, pair.BackwardBillId, _anna.Id, false, default);

        Assert.Equal(BillActionStatus.Ok, result.Status);
        Assert.Equal(ProcessingState.Ready, LoadBill(pair.ForwardBillId).Status);
        Assert.Equal(ProcessingState.Ready, LoadBill(pair.BackwardBillId).Status);
        await _f.Publish.Received(1).Publish(Arg.Is<BillFinalized>(m => m.id == pair.ForwardBillId), Arg.Any<CancellationToken>());
        await _f.Publish.Received(1).Publish(Arg.Is<BillFinalized>(m => m.id == pair.BackwardBillId), Arg.Any<CancellationToken>());

        // A second accept is rejected
        var again = await _f.BillService.FinalizeExchangeAsync(pair.ForwardBillId, pair.BackwardBillId, _anna.Id, false, default);
        Assert.Equal(BillActionStatus.Invalid, again.Status);
    }

    [Fact]
    public async Task FinalizeExchange_ChecksAccess()
    {
        var pair = _f.BillService.CreateExchange(Exchange(), _anna.Id).Value!;
        _f.Clear();

        Assert.Equal(BillActionStatus.NotFound,
            (await _f.BillService.FinalizeExchangeAsync(pair.ForwardBillId, pair.BackwardBillId, _stranger.Id, false, default)).Status);
        Assert.Equal(BillActionStatus.Forbidden,
            (await _f.BillService.FinalizeExchangeAsync(pair.ForwardBillId, pair.BackwardBillId, _boris.Id, false, default)).Status);
        Assert.Equal(ProcessingState.Draft, LoadBill(pair.ForwardBillId).Status);

        Assert.Equal(BillActionStatus.Ok,
            (await _f.BillService.FinalizeExchangeAsync(pair.ForwardBillId, pair.BackwardBillId, _admin.Id, true, default)).Status);
    }

    [Fact]
    public async Task FinalizeExchange_RejectsBillsThatAreNotAPair()
    {
        var first = _f.BillService.CreateExchange(Exchange(), _anna.Id).Value!;
        var second = _f.BillService.CreateExchange(Exchange(), _anna.Id).Value!;
        _f.Clear();

        var result = await _f.BillService.FinalizeExchangeAsync(first.ForwardBillId, second.BackwardBillId, _anna.Id, false, default);

        Assert.Equal(BillActionStatus.Invalid, result.Status);
        Assert.Equal(ProcessingState.Draft, LoadBill(first.ForwardBillId).Status);
    }

    [Fact]
    public async Task CancelExchangeBill_CancelsItsPairToo()
    {
        var pair = _f.BillService.CreateExchange(Exchange(), _anna.Id).Value!;
        _f.Clear();

        Assert.Equal(BillActionStatus.Ok, (await _f.BillService.CancelAsync(pair.ForwardBillId, _anna.Id, false, default)).Status);

        await _f.Publish.Received(1).Publish(Arg.Is<BillCanceled>(m => m.BillId == pair.ForwardBillId), Arg.Any<CancellationToken>());
        await _f.Publish.Received(1).Publish(Arg.Is<BillCanceled>(m => m.BillId == pair.BackwardBillId), Arg.Any<CancellationToken>());
    }
}
