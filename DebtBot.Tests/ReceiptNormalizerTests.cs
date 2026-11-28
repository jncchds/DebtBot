using DebtBot.Models.Receipt;
using DebtBot.Receipts;
using DebtBot.Services;
using System.Text.Json;

namespace DebtBot.Tests;

/// <summary>Receipts T1: schema validation, normalisation (receipt-parse.md, "Normalisation") and the text builder.</summary>
public class ReceiptNormalizerTests
{
    private static JsonElement Answer(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static JsonElement Fixture(string name) => Answer(FakeLlmClient.Fixture(name));

    private static ReceiptDraft Normalize(string fixture, string? lastBillCurrency = null) =>
        ReceiptNormalizer.Normalize(Fixture(fixture), lastBillCurrency);

    private static string[] Codes(ReceiptDraft draft) => draft.Warnings.Select(w => w.Code).ToArray();

    private static ReceiptWarning Warning(ReceiptDraft draft, string code) => Assert.Single(draft.Warnings, w => w.Code == code);

    public static TheoryData<string> ReceiptFixtures => new()
    {
        "cafe-eur", "supermarket-discount", "decimal-comma", "dollar-ambiguous", "tips-outside",
    };

    // --- numbers ---

    [Theory]
    [InlineData("13.80", "13.80")]
    [InlineData("1.234,50", "1234.50")]
    [InlineData("1,234.50", "1234.50")]
    [InlineData("4 574,50", "4574.50")]
    [InlineData("4 574,50", "4574.50")]
    [InlineData("1'234.50", "1234.50")]
    [InlineData("12,5", "12.5")]
    [InlineData("7,00", "7.00")]
    [InlineData("1,234", "1234")]
    [InlineData("1,234,567", "1234567")]
    [InlineData("1.234.567", "1234567")]
    [InlineData("1.234.567,89", "1234567.89")]
    [InlineData("€ 7,00", "7.00")]
    [InlineData("12.00 EUR", "12.00")]
    [InlineData("-1.00", "-1.00")]
    [InlineData("1.00-", "-1.00")]
    [InlineData("−2,50", "-2.50")]
    [InlineData("(2.50)", "-2.50")]
    [InlineData("0", "0")]
    public void Amounts_take_either_separator(string raw, string expected)
    {
        Assert.True(ReceiptNormalizer.TryParseAmount(raw, out var value));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("€")]
    [InlineData(".,")]
    public void Unreadable_amounts_are_refused(string? raw)
    {
        Assert.False(ReceiptNormalizer.TryParseAmount(raw, out _));
    }

    [Fact]
    public void Amounts_never_pass_through_a_float_and_round_to_minor_units()
    {
        var draft = ReceiptNormalizer.Normalize(Answer("""
            { "isReceipt": true, "currency": "JPY", "items": [ { "name": "Ramen", "amount": "1,280" }, { "name": "Gyoza", "amount": 480.4 } ], "total": "1760" }
            """), null);
        Assert.Equal([1280m, 480m], draft.Lines.Select(l => l.Subtotal));
        Assert.Equal(1760m, draft.TotalWithTips);

        var kwd = ReceiptNormalizer.Normalize(Answer("""
            { "isReceipt": true, "currency": "KWD", "items": [ { "name": "Tea", "amount": "1.2345" } ], "total": "1.2345" }
            """), null);
        Assert.Equal(1.235m, kwd.Lines[0].Subtotal);

        var eur = ReceiptNormalizer.Normalize(Answer("""
            { "isReceipt": true, "currency": "EUR", "items": [ { "name": "Big number", "amount": 0.1000000000000000055511151231257827 } ], "total": "0.10" }
            """), null);
        Assert.Equal(0.10m, eur.Lines[0].Subtotal);
    }

    // --- currency ---

    [Theory]
    [InlineData("EUR", null, "EUR")]
    [InlineData("eur", "$", "EUR")]
    [InlineData(null, "€", "EUR")]
    [InlineData(null, "£", "GBP")]
    [InlineData(null, "₽", "RUB")]
    [InlineData("руб", null, "RUB")]
    [InlineData(null, "₸", "KZT")]
    [InlineData(null, "₺", "TRY")]
    [InlineData(null, "R$", "BRL")]
    [InlineData(null, "US$", "USD")]
    [InlineData("ABC", "€", "EUR")]
    public void Currency_is_the_iso_code_else_the_sign(string? currency, string? symbol, string expected)
    {
        var warnings = new List<ReceiptWarning>();
        Assert.Equal(expected, ReceiptNormalizer.ResolveCurrency(currency, symbol, "GBP", warnings));
        Assert.Empty(warnings);
    }

    [Fact]
    public void An_ambiguous_dollar_takes_the_last_bill_currency_when_it_is_a_dollar()
    {
        var cad = Normalize("dollar-ambiguous", "CAD");
        Assert.Equal("CAD", cad.CurrencyCode);
        var w = Warning(cad, ReceiptWarnings.CurrencyFromLastBill);
        Assert.Equal("$", w.Params["symbol"]);
        Assert.Equal("CAD", w.Params["currency"]);

        Assert.Equal("USD", Normalize("dollar-ambiguous", "usd").CurrencyCode);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData(null)]
    public void An_ambiguous_dollar_otherwise_stays_empty_with_a_warning(string? lastBill)
    {
        var draft = Normalize("dollar-ambiguous", lastBill);
        Assert.Null(draft.CurrencyCode);
        Assert.Equal("$", Warning(draft, ReceiptWarnings.CurrencyAmbiguous).Params["symbol"]);
        Assert.DoesNotContain(ReceiptWarnings.CurrencyFromLastBill, Codes(draft));
    }

    [Fact]
    public void No_currency_at_all_stays_empty()
    {
        var warnings = new List<ReceiptWarning>();
        Assert.Null(ReceiptNormalizer.ResolveCurrency(null, null, "EUR", warnings));
        Assert.Equal(ReceiptWarnings.CurrencyMissing, Assert.Single(warnings).Code);

        warnings.Clear();
        Assert.Null(ReceiptNormalizer.ResolveCurrency(null, "¤", "EUR", warnings));
        Assert.Equal("¤", Assert.Single(warnings).Params["symbol"]);
    }

    [Fact]
    public void A_yen_sign_is_ambiguous_too()
    {
        var warnings = new List<ReceiptWarning>();
        Assert.Equal("CNY", ReceiptNormalizer.ResolveCurrency(null, "¥", "CNY", warnings));
        Assert.Null(ReceiptNormalizer.ResolveCurrency(null, "¥", "USD", warnings));
    }

    // --- the fixtures ---

    [Fact]
    public void Cafe_in_eur()
    {
        var draft = Normalize("cafe-eur");
        Assert.Equal("Corner Café", draft.Description); // a time is printed: no date in the description
        Assert.Equal(new DateTime(2026, 11, 20, 19, 42, 0, DateTimeKind.Utc), draft.Date);
        Assert.Equal("EUR", draft.CurrencyCode);
        Assert.Equal(39.50m, draft.TotalWithTips);
        Assert.Equal(["Borscht ×2", "Pelmeni", "Kvass ×2", "Napoleon cake"], draft.Lines.Select(l => l.Description));
        Assert.Equal([13.80m, 12.50m, 7.00m, 6.20m], draft.Lines.Select(l => l.Subtotal));
        Assert.Empty(draft.Warnings);
    }

    [Fact]
    public void Supermarket_with_a_discount_line()
    {
        var draft = Normalize("supermarket-discount");
        Assert.Equal("FreshMart 2026-11-21", draft.Description); // only a date printed
        Assert.Equal(new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc), draft.Date);
        Assert.Equal("EUR", draft.CurrencyCode); // from the sign
        Assert.Equal("Apples ×1.235", draft.Lines[3].Description);
        Assert.Equal(-1.00m, draft.Lines[4].Subtotal); // kept as printed, never "fixed"
        Assert.Equal(12.06m, draft.TotalWithTips);
        var discount = Assert.Single(draft.Warnings);
        Assert.Equal(ReceiptWarnings.DiscountLine, discount.Code);
        Assert.Equal("5", discount.Params["line"]);
        Assert.Equal("1.00", discount.Params["amount"]);
    }

    [Fact]
    public void Decimal_comma_with_a_service_charge_outside_the_total()
    {
        var draft = Normalize("decimal-comma");
        Assert.Equal("RUB", draft.CurrencyCode);
        Assert.Equal([2450.00m, 1234.50m, 890.00m], draft.Lines.Select(l => l.Subtotal));
        Assert.Equal("Салат ×2", draft.Lines[2].Description);
        Assert.Equal(4574.50m + 457.45m, draft.TotalWithTips);
        Assert.Equal("457.45", Warning(draft, ReceiptWarnings.ServiceChargeAdded).Params["amount"]);
        Assert.DoesNotContain(ReceiptWarnings.LinesTotalMismatch, Codes(draft));
    }

    [Fact]
    public void Tax_on_top_of_the_items_is_not_a_mismatch()
    {
        var draft = Normalize("dollar-ambiguous", "USD");
        Assert.Equal(20.62m, draft.TotalWithTips);
        Assert.Equal(19.00m, draft.Lines.Sum(l => l.Subtotal));
        Assert.DoesNotContain(ReceiptWarnings.LinesTotalMismatch, Codes(draft));
    }

    [Fact]
    public void Tips_outside_the_total_are_added_to_it()
    {
        var draft = Normalize("tips-outside");
        Assert.Equal(35.00m, draft.TotalWithTips);
        Assert.Equal("4.00", Warning(draft, ReceiptWarnings.TipsAdded).Params["amount"]);
        Assert.DoesNotContain(ReceiptWarnings.LinesTotalMismatch, Codes(draft));
    }

    [Fact]
    public void Tips_inside_the_total_are_not_added()
    {
        var draft = ReceiptNormalizer.Normalize(Answer("""
            { "isReceipt": true, "currency": "EUR", "items": [ { "name": "Soup", "amount": "10.00" } ], "tips": "1.00", "tipsIncluded": true, "total": "11.00" }
            """), null);
        Assert.Equal(11.00m, draft.TotalWithTips);
        Assert.Empty(draft.Warnings);
    }

    [Fact]
    public void Lines_off_the_total_by_more_than_one_percent_give_a_warning()
    {
        var draft = ReceiptNormalizer.Normalize(Answer("""
            { "isReceipt": true, "currency": "EUR", "items": [ { "name": "A", "amount": "20.00" }, { "name": "B", "amount": "21.50" } ], "total": "45.00" }
            """), null);
        var w = Warning(draft, ReceiptWarnings.LinesTotalMismatch);
        Assert.Equal("41.50", w.Params["linesSum"]);
        Assert.Equal("45.00", w.Params["total"]);
        Assert.Equal("3.50", w.Params["difference"]);
        Assert.Equal(45.00m, draft.TotalWithTips); // never "fixed"
        Assert.Equal([20.00m, 21.50m], draft.Lines.Select(l => l.Subtotal));

        var within = ReceiptNormalizer.Normalize(Answer("""
            { "isReceipt": true, "currency": "EUR", "items": [ { "name": "A", "amount": "99.50" } ], "total": "100.00" }
            """), null);
        Assert.Empty(within.Warnings);
    }

    [Fact]
    public void No_total_leaves_the_total_empty()
    {
        var draft = ReceiptNormalizer.Normalize(Answer("""
            { "isReceipt": true, "currency": "EUR", "items": [ { "name": "A", "amount": "2.00" } ], "total": null }
            """), null);
        Assert.Null(draft.TotalWithTips);
        Assert.Equal(ReceiptWarnings.TotalMissing, Assert.Single(draft.Warnings).Code);
    }

    [Fact]
    public void Negative_amounts_outside_the_lines_are_ignored()
    {
        var draft = ReceiptNormalizer.Normalize(Answer("""
            { "isReceipt": true, "currency": "EUR", "items": [ { "name": "A", "amount": "2.00" } ], "tips": "-1.00", "tipsIncluded": false, "total": "2.00" }
            """), null);
        Assert.Equal(2.00m, draft.TotalWithTips);
        Assert.Equal("tips", Warning(draft, ReceiptWarnings.NegativeAmountIgnored).Params["field"]);
    }

    // --- limits ---

    [Fact]
    public void At_most_100_items()
    {
        var items = string.Join(",", Enumerable.Range(1, 105).Select(i => $$"""{ "name": "Item {{i}}", "amount": "1.00" }"""));
        var draft = ReceiptNormalizer.Normalize(Answer($$"""{ "isReceipt": true, "currency": "EUR", "items": [{{items}}], "total": "105.00" }"""), null);
        Assert.Equal(100, draft.Lines.Count);
        Assert.Equal("Item 100", draft.Lines[^1].Description);
        var w = Warning(draft, ReceiptWarnings.TooManyItems);
        Assert.Equal("105", w.Params["count"]);
        Assert.Equal("100", w.Params["kept"]);
    }

    [Fact]
    public void Names_are_one_line_cut_to_the_line_description_length()
    {
        var longName = new string('x', 250);
        var draft = ReceiptNormalizer.Normalize(Answer($$"""
            { "isReceipt": true, "currency": "EUR", "items": [
              { "name": "{{longName}}", "amount": "1.00" },
              { "name": "Two\nlines\t here", "amount": "1.00" },
              { "name": "", "amount": "1.00" },
              { "name": "No amount", "amount": null },
              { "name": "Bad amount", "amount": "n/a" }
            ], "total": "3.00" }
            """), null);
        Assert.Equal(ReceiptNormalizer.MaxLineDescriptionLength, draft.Lines[0].Description.Length);
        Assert.Equal("1", Warning(draft, ReceiptWarnings.NameTruncated).Params["line"]);
        Assert.Equal("Two lines here", draft.Lines[1].Description);
        Assert.Equal("Item 3", draft.Lines[2].Description);
        Assert.Equal(3, draft.Lines.Count);
        Assert.Equal(["No amount", "Bad amount"], draft.Warnings.Where(w => w.Code == ReceiptWarnings.ItemSkipped).Select(w => w.Params["name"]));
    }

    [Fact]
    public void Description_falls_back_and_dates_are_read_as_printed()
    {
        var draft = ReceiptNormalizer.Normalize(Answer("""{ "isReceipt": true, "merchant": null, "date": "yesterday", "items": [] }"""), null);
        Assert.Equal("Receipt", draft.Description);
        Assert.Null(draft.Date);
        Assert.Equal("yesterday", Warning(draft, ReceiptWarnings.DateUnreadable).Params["value"]);

        Assert.True(ReceiptNormalizer.TryParseDate("2026-11-20 19:42:10", out var withSeconds, out var hasTime));
        Assert.True(hasTime);
        Assert.Equal(new DateTime(2026, 11, 20, 19, 42, 10), withSeconds);
        Assert.True(ReceiptNormalizer.TryParseDate("2026-11-20T19:42:00.000+03:00", out var withOffset, out _));
        Assert.Equal(new DateTime(2026, 11, 20, 19, 42, 0), withOffset);
        Assert.False(ReceiptNormalizer.TryParseDate("1900-01-01", out _, out _));
        Assert.False(ReceiptNormalizer.TryParseDate("20.11.2026", out _, out _));
    }

    // --- schema ---

    [Fact]
    public void Every_receipt_fixture_fits_the_schema()
    {
        foreach (var name in ReceiptFixtures)
        {
            Assert.True(ReceiptSchema.IsValid(Fixture(name)), name);
        }
        Assert.True(ReceiptSchema.IsNotAReceipt(Fixture("not-a-receipt")));
        Assert.Null(LlmJson.ParseObject(FakeLlmClient.Fixture("broken-json")));
    }

    [Theory]
    [InlineData("""{ "merchant": "x", "items": [] }""")] // no isReceipt
    [InlineData("""{ "isReceipt": true }""")] // no items
    [InlineData("""{ "isReceipt": "yes", "items": [] }""")]
    [InlineData("""{ "isReceipt": true, "items": {} }""")]
    [InlineData("""{ "isReceipt": true, "items": [ { "name": "A", "amount": { "value": 1 } } ] }""")]
    [InlineData("""{ "isReceipt": true, "items": [], "total": true }""")]
    public void Answers_off_the_schema_are_invalid(string json)
    {
        Assert.False(ReceiptSchema.IsValid(Answer(json)));
    }

    [Fact]
    public void The_lenient_schema_takes_numbers_and_extra_fields()
    {
        Assert.True(ReceiptSchema.IsValid(Answer("""{ "isReceipt": true, "items": [ { "name": "A", "amount": 1.5, "vat": "A" } ], "total": 1.5, "cashier": "Olga" }""")));
        Assert.False(ReceiptSchema.IsNotAReceipt(Answer("""{ "isReceipt": true, "items": [] }""")));
    }

    [Fact]
    public void The_strict_schema_is_usable_in_strict_mode()
    {
        var strict = ReceiptSchema.Strict();
        var required = strict["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToHashSet();
        var properties = strict["properties"]!.AsObject().Select(p => p.Key).ToHashSet();
        Assert.Equal(properties, required); // strict mode: every property is required
        Assert.False(strict["additionalProperties"]!.GetValue<bool>());
        Assert.Contains(ReceiptSchema.StrictJson, ReceiptSchema.SystemPrompt); // json_object models get it in the prompt
    }

    // --- the text builder ---

    [Theory]
    [MemberData(nameof(ReceiptFixtures))]
    public void Text_round_trips_through_the_parser(string fixture)
    {
        var draft = Normalize(fixture, "USD");
        var text = ReceiptTextBuilder.Build(draft, "@anna");

        var parsed = new ParserService(null!).ParseBill(Guid.NewGuid(), text);

        Assert.True(parsed.IsValid, string.Join("; ", parsed.Errors));
        var bill = parsed.Result!;
        Assert.Equal(draft.Description, bill.Description);
        Assert.Equal(draft.CurrencyCode ?? ReceiptTextBuilder.UnknownCurrency, bill.CurrencyCode);
        var total = draft.TotalWithTips ?? draft.Lines.Sum(l => l.Subtotal);
        Assert.Equal(total, bill.TotalWithTips);
        var payment = Assert.Single(bill.Payments);
        Assert.Equal(total, payment.Amount);
        Assert.Equal("@anna", payment.User.QueryString);
        Assert.Equal(draft.Lines.Select(l => ((string?)l.Description, (decimal?)l.Subtotal)), bill.Lines.Select(l => (l.ItemDescription, l.Subtotal)));
        Assert.All(bill.Lines, l =>
        {
            var p = Assert.Single(l.Participants);
            Assert.Equal(1m, p.Part);
            Assert.Equal("@anna", p.User.QueryString);
        });
    }

    [Fact]
    public void Text_for_the_cafe()
    {
        var text = ReceiptTextBuilder.Build(Normalize("cafe-eur"), "@anna");
        Assert.Equal("""
            Corner Café

            39.50
            EUR

            39.50 @anna

            Borscht ×2
            13.80
            1 @anna

            Pelmeni
            12.50
            1 @anna

            Kvass ×2
            7.00
            1 @anna

            Napoleon cake
            6.20
            1 @anna
            """, text);
    }

    [Fact]
    public void Text_without_currency_or_total_still_parses()
    {
        var draft = new ReceiptDraft
        {
            Description = "Shop\n\nwith a blank line",
            Lines = [new() { Description = "A", Subtotal = 2.5m }, new() { Description = "  ", Subtotal = 1m }],
        };
        var text = ReceiptTextBuilder.Build(draft, "1001");
        var parsed = new ParserService(null!).ParseBill(Guid.NewGuid(), text);
        Assert.True(parsed.IsValid, string.Join("; ", parsed.Errors));
        Assert.Equal("Shop with a blank line", parsed.Result!.Description);
        Assert.Equal("XXX", parsed.Result.CurrencyCode);
        Assert.Equal(3.5m, parsed.Result.TotalWithTips);
        Assert.Equal(["A", "Item"], parsed.Result.Lines.Select(l => l.ItemDescription));
    }

    [Fact]
    public void The_sender_is_the_username_as_stored_else_the_telegram_id()
    {
        var id = Guid.NewGuid();
        Assert.Equal("@anna", ReceiptTextBuilder.Sender("@anna", 1001, id));
        Assert.Equal("anna", ReceiptTextBuilder.Sender("anna", 1001, id));
        Assert.Equal("1001", ReceiptTextBuilder.Sender(null, 1001, id));
        Assert.Equal("1001", ReceiptTextBuilder.Sender("  ", 1001, id));
        Assert.Equal(id.ToString(), ReceiptTextBuilder.Sender(null, null, id));
    }
}
