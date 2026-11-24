using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;
using DebtBot.Models.Enums;
using DebtBot.Services;

namespace DebtBot.Tests;

public class BillRulesTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Boris = Guid.NewGuid();
    private static readonly Guid Clara = Guid.NewGuid();

    private static BillLineCreationModel Line(decimal subtotal, params (Guid user, decimal? part)[] participants) => new()
    {
        ItemDescription = "Pizza",
        Subtotal = subtotal,
        Participants = participants.Select(p => new BillLineParticipantCreationModel { UserId = p.user, Part = p.part }).ToList(),
    };

    [Fact]
    public void EqualSplit_EveryParticipantGetsPartOne()
    {
        var lines = BillRules.ToParserLines([Line(30m, (Anna, null), (Boris, null), (Clara, null))]);

        var line = Assert.Single(lines);
        Assert.Equal("Pizza", line.ItemDescription);
        Assert.Equal(30m, line.Subtotal);
        Assert.Equal([1m, 1m, 1m], line.Participants.Select(p => p.Part!.Value));
        Assert.Equal([Anna, Boris, Clara], line.Participants.Select(p => p.User.Id!.Value));
    }

    [Fact]
    public void SplitByParts_KeepsTheParts()
    {
        var lines = BillRules.ToParserLines([Line(30m, (Anna, 2m), (Boris, 0.5m))]);

        Assert.Equal([2m, 0.5m], lines[0].Participants.Select(p => p.Part!.Value));
    }

    [Fact]
    public void Payments_MapToUserIdAndAmount()
    {
        var payments = BillRules.ToParserPayments([new BillPaymentCreationModel { UserId = Anna, Amount = 12.5m }]);

        var payment = Assert.Single(payments);
        Assert.Equal(Anna, payment.User.Id);
        Assert.Equal(12.5m, payment.Amount);
    }

    [Fact]
    public void ToParserModel_FillsDefaults()
    {
        var now = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
        var model = BillRules.ToParserModel(new BillCreationModel
        {
            CurrencyCode = "eur",
            Lines = [Line(10m, (Anna, null)), Line(5.5m, (Boris, null))],
        }, now);

        Assert.Equal("EUR", model.CurrencyCode);
        Assert.Equal("EUR", model.PaymentCurrencyCode);
        Assert.Equal(15.5m, model.TotalWithTips);
        Assert.Equal(now, model.Date);
        Assert.Equal("", model.Description);
        Assert.Empty(model.Payments);
    }

    [Fact]
    public void ToParserModel_KeepsGivenValues()
    {
        var date = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var model = BillRules.ToParserModel(new BillCreationModel
        {
            CurrencyCode = "UAH",
            PaymentCurrencyCode = "pln",
            TotalWithTips = 120m,
            Description = "Dinner",
            Date = date,
            Lines = [Line(100m, (Anna, null))],
        }, DateTime.UtcNow);

        Assert.Equal("PLN", model.PaymentCurrencyCode);
        Assert.Equal(120m, model.TotalWithTips);
        Assert.Equal("Dinner", model.Description);
        Assert.Equal(date, model.Date);
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("")]
    public void ValidateBill_RejectsBadCurrency(string currency)
    {
        Assert.NotEmpty(BillRules.ValidateBill(new BillCreationModel { CurrencyCode = currency }));
    }

    [Fact]
    public void ValidateBill_AcceptsAnEmptyDraft()
    {
        Assert.Empty(BillRules.ValidateBill(new BillCreationModel { CurrencyCode = "EUR" }));
    }

    [Fact]
    public void ValidateLines_RejectsBadLines()
    {
        Assert.NotEmpty(BillRules.ValidateLines([Line(0m, (Anna, null))]));
        Assert.NotEmpty(BillRules.ValidateLines([Line(10m)]));
        Assert.NotEmpty(BillRules.ValidateLines([Line(10m, (Anna, 0m))]));
        Assert.NotEmpty(BillRules.ValidateLines([Line(10m, (Anna, -1m))]));
        Assert.NotEmpty(BillRules.ValidateLines([Line(10m, (Anna, null), (Anna, 2m))]));
        Assert.NotEmpty(BillRules.ValidateLines([Line(10m, (Guid.Empty, null))]));
        Assert.Empty(BillRules.ValidateLines([Line(10m, (Anna, null), (Boris, 3m))]));
    }

    [Fact]
    public void ValidatePayments_RejectsBadPayments()
    {
        Assert.NotEmpty(BillRules.ValidatePayments([new BillPaymentCreationModel { UserId = Anna, Amount = 0m }]));
        Assert.NotEmpty(BillRules.ValidatePayments([new BillPaymentCreationModel { UserId = Guid.Empty, Amount = 1m }]));
        Assert.NotEmpty(BillRules.ValidatePayments(
        [
            new BillPaymentCreationModel { UserId = Anna, Amount = 1m },
            new BillPaymentCreationModel { UserId = Anna, Amount = 2m },
        ]));
        Assert.Empty(BillRules.ValidatePayments([new BillPaymentCreationModel { UserId = Anna, Amount = 1m }]));
    }

    [Fact]
    public void ValidateForFinalize_NeedsLinesPaymentsAndDraft()
    {
        var bill = new BillModel
        {
            Status = ProcessingState.Draft,
            TotalWithTips = 10m,
            Lines = [],
            Payments = [],
        };
        Assert.Equal(2, BillRules.ValidateForFinalize(bill).Count);

        bill.Lines = [new BillLineModel { Subtotal = 10m, Participants = [new() { Part = 1m }] }];
        bill.Payments = [new BillPaymentModel { Amount = 10m }];
        Assert.Empty(BillRules.ValidateForFinalize(bill));

        bill.Status = ProcessingState.Processed;
        Assert.NotEmpty(BillRules.ValidateForFinalize(bill));
    }
}
