using DebtBot.Models.Bill;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
using DebtBot.Services;

namespace DebtBot.Tests;

public class ExchangeRulesTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Boris = Guid.NewGuid();

    private static ExchangeCreationModel Exchange(decimal fromAmount = 100m, decimal toAmount = 400m, Guid? to = null) => new()
    {
        FromUserId = Anna,
        FromAmount = fromAmount,
        FromCurrencyCode = "eur",
        ToUserId = to ?? Boris,
        ToAmount = toAmount,
        ToCurrencyCode = "pln",
    };

    [Fact]
    public void Validate_AcceptsAGoodExchange()
    {
        Assert.Empty(ExchangeRules.Validate(Exchange()));
    }

    [Theory]
    [InlineData(0, 400)]
    [InlineData(100, 0)]
    [InlineData(-5, 400)]
    [InlineData(100, -1)]
    public void Validate_AmountsMustBeAboveZero(decimal from, decimal to)
    {
        Assert.Contains("Amounts must be above zero", ExchangeRules.Validate(Exchange(from, to)));
    }

    [Fact]
    public void Validate_NeedsTwoDifferentUsers()
    {
        Assert.Contains("An exchange needs two different users", ExchangeRules.Validate(Exchange(to: Anna)));
        Assert.Contains("Both users are needed", ExchangeRules.Validate(Exchange(to: Guid.Empty)));
    }

    [Fact]
    public void Validate_NeedsCurrencyCodes()
    {
        var exchange = Exchange();
        exchange.ToCurrencyCode = "zl";
        Assert.Contains("Currency codes must be 3 letters", ExchangeRules.Validate(exchange));
    }

    [Fact]
    public void BuildBills_MirrorsTheTelegramExchange()
    {
        var now = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

        var (forward, backward) = ExchangeRules.BuildBills(Exchange(), now);

        // Forward: Anna pays 100 EUR for Boris's 400 PLN line -> Boris owes Anna 100 EUR.
        Assert.Equal("PLN", forward.CurrencyCode);
        Assert.Equal("EUR", forward.PaymentCurrencyCode);
        Assert.Equal(400m, forward.TotalWithTips);
        Assert.Equal(now, forward.Date);
        var forwardLine = Assert.Single(forward.Lines);
        Assert.Equal("Exchange", forwardLine.ItemDescription);
        Assert.Equal(400m, forwardLine.Subtotal);
        Assert.Equal(Boris, Assert.Single(forwardLine.Participants).User.Id);
        var forwardPayment = Assert.Single(forward.Payments);
        Assert.Equal(Anna, forwardPayment.User.Id);
        Assert.Equal(100m, forwardPayment.Amount);

        // Backward: Boris pays 400 PLN for Anna's 100 EUR line -> Anna owes Boris 400 PLN.
        Assert.Equal("EUR", backward.CurrencyCode);
        Assert.Equal("PLN", backward.PaymentCurrencyCode);
        Assert.Equal(100m, backward.TotalWithTips);
        Assert.Equal(Anna, Assert.Single(Assert.Single(backward.Lines).Participants).User.Id);
        var backwardPayment = Assert.Single(backward.Payments);
        Assert.Equal(Boris, backwardPayment.User.Id);
        Assert.Equal(400m, backwardPayment.Amount);

        Assert.Equal("Exchange\nExchange 0.25 EUR/PLN", forward.Description);
        Assert.Equal("Exchange\nExchange 4 PLN/EUR", backward.Description);
    }

    private static BillModel Bill(Guid id, Guid? pair, ProcessingState status = ProcessingState.Draft) =>
        new() { Id = id, ExchangeBillId = pair, Status = status };

    [Fact]
    public void ValidatePair_ChecksTheLinkAndStatus()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Assert.Empty(ExchangeRules.ValidatePair(Bill(a, b), Bill(b, a)));
        // Exchanges made before the link existed
        Assert.Empty(ExchangeRules.ValidatePair(Bill(a, null), Bill(b, null)));

        Assert.NotEmpty(ExchangeRules.ValidatePair(Bill(a, Guid.NewGuid()), Bill(b, a)));
        Assert.NotEmpty(ExchangeRules.ValidatePair(Bill(a, b), Bill(a, b)));
        Assert.NotEmpty(ExchangeRules.ValidatePair(Bill(a, b), Bill(b, a, ProcessingState.Processed)));
    }
}
