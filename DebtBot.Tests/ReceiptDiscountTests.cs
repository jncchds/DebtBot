using DebtBot.Models.Receipt;
using DebtBot.Receipts;
using DebtBot.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DebtBot.Tests;

/// <summary>Receipts T2: discount lines folded into the other lines (user decision, 2026-11-28).</summary>
public class ReceiptDiscountTests
{
    private static ReceiptDraft Normalize(string currency, string? total, params (string Name, string Amount)[] items)
    {
        var answer = new JsonObject
        {
            ["isReceipt"] = true,
            ["merchant"] = "Shop",
            ["currency"] = currency,
            ["total"] = total,
            ["items"] = new JsonArray(items.Select(i => (JsonNode)new JsonObject { ["name"] = i.Name, ["amount"] = i.Amount }).ToArray()),
        };
        return ReceiptNormalizer.Normalize(JsonDocument.Parse(answer.ToJsonString()).RootElement, null);
    }

    private static decimal[] Amounts(ReceiptDraft draft) => draft.Lines.Select(l => l.Subtotal).ToArray();
    private static string[] Names(ReceiptDraft draft) => draft.Lines.Select(l => l.Description).ToArray();
    private static string[] Codes(ReceiptDraft draft) => draft.Warnings.Select(w => w.Code).ToArray();

    [Fact]
    public void An_item_discount_is_taken_off_the_item_above()
    {
        var draft = Normalize("EUR", "13.00", ("Pizza", "10.00"), ("Discount", "-2.00"), ("Beer", "5.00"));

        Assert.Equal(["Pizza", "Beer"], Names(draft));
        Assert.Equal([8.00m, 5.00m], Amounts(draft));
        var warning = Assert.Single(draft.Warnings);
        Assert.Equal(ReceiptWarnings.DiscountFolded, warning.Code);
        Assert.Equal("Discount", warning.Params["name"]);
        Assert.Equal("Pizza", warning.Params["item"]);
        Assert.Equal("2.00", warning.Params["amount"]);
        Assert.Equal("The discount “Discount” (2.00 EUR) was taken off “Pizza”.", warning.Message);
    }

    [Fact]
    public void A_whole_receipt_discount_is_spread_with_exact_cents()
    {
        var draft = Normalize("EUR", "29.00", ("A", "10.00"), ("B", "10.00"), ("C", "10.00"), ("Coupon", "-1.00"));

        // 0.33 off each, and the remaining cent off the largest (the first of equals).
        Assert.Equal([9.66m, 9.67m, 9.67m], Amounts(draft));
        Assert.Equal(29.00m, draft.Lines.Sum(l => l.Subtotal));
        Assert.Equal(29.00m, draft.TotalWithTips); // never changed
        var warning = Assert.Single(draft.Warnings);
        Assert.Equal(ReceiptWarnings.DiscountSpread, warning.Code);
        Assert.Equal("Coupon", warning.Params["name"]);
        Assert.Equal("1.00", warning.Params["amount"]);
    }

    [Theory]
    [InlineData("Loyalty card")]
    [InlineData("Coupon 2026")]
    [InlineData("Bonus points")]
    [InlineData("Скидка по карте")]
    [InlineData("Gutschein")]
    public void Whole_receipt_words_spread_even_right_under_an_item(string name)
    {
        var draft = Normalize("EUR", "9.10", ("A", "6.00"), ("B", "3.00"), (name, "-0.90"), ("C", "1.00"));

        Assert.Equal([5.46m, 2.73m, 0.91m], Amounts(draft));
        Assert.Equal(ReceiptWarnings.DiscountSpread, Assert.Single(draft.Warnings).Code);
    }

    [Fact]
    public void A_discount_before_any_item_is_spread()
    {
        var draft = Normalize("EUR", "9.00", ("Discount", "-1.00"), ("A", "5.00"), ("B", "5.00"));

        Assert.Equal([4.50m, 4.50m], Amounts(draft));
        Assert.Equal(ReceiptWarnings.DiscountSpread, Assert.Single(draft.Warnings).Code);
    }

    [Fact]
    public void A_plain_discount_after_the_last_item_is_the_receipts()
    {
        var draft = Normalize("EUR", "9.00", ("A", "4.00"), ("B", "6.00"), ("Discount 10%", "-1.00"));

        Assert.Equal([3.60m, 5.40m], Amounts(draft));
        Assert.Equal(ReceiptWarnings.DiscountSpread, Assert.Single(draft.Warnings).Code);
    }

    [Fact]
    public void A_discount_bigger_than_the_item_above_is_spread_instead()
    {
        var draft = Normalize("EUR", "6.50", ("Coffee", "3.00"), ("Cake", "2.00"), ("Cake offer", "-2.50"), ("Tea", "4.00"));

        Assert.Equal(["Coffee", "Cake", "Tea"], Names(draft));
        Assert.Equal([2.17m, 1.44m, 2.89m], Amounts(draft));
        Assert.Equal(6.50m, draft.Lines.Sum(l => l.Subtotal));
        Assert.Equal(ReceiptWarnings.DiscountSpread, Assert.Single(draft.Warnings).Code);
    }

    [Fact]
    public void A_discount_as_big_as_the_item_above_is_spread_too()
    {
        // Folding would leave the cake at 0, which the form refuses.
        var draft = Normalize("EUR", "8.00", ("Coffee", "3.00"), ("Cake", "2.00"), ("Cake offer", "-2.00"), ("Tea", "5.00"));

        Assert.Equal([2.40m, 1.60m, 4.00m], Amounts(draft));
        Assert.Equal(ReceiptWarnings.DiscountSpread, Assert.Single(draft.Warnings).Code);
    }

    [Fact]
    public void Several_discounts()
    {
        var draft = Normalize("EUR", "8.25",
            ("Milk", "2.00"), ("Discount", "-0.50"),
            ("Bread", "3.00"), ("Cheese", "5.00"),
            ("Loyalty card", "-0.80"), ("Discount 5%", "-0.45"));

        Assert.Equal(["Milk", "Bread", "Cheese"], Names(draft));
        // Milk 2.00 − 0.50 = 1.50, then 1.25 spread over 1.50, 3.00 and 5.00.
        Assert.Equal([1.30m, 2.61m, 4.34m], Amounts(draft));
        Assert.Equal(8.25m, draft.Lines.Sum(l => l.Subtotal));
        Assert.Equal([ReceiptWarnings.DiscountFolded, ReceiptWarnings.DiscountSpread, ReceiptWarnings.DiscountSpread], Codes(draft));
        Assert.Equal(["0.50", "0.80", "0.45"], draft.Warnings.Select(w => w.Params["amount"]));
        Assert.DoesNotContain(ReceiptWarnings.LinesTotalMismatch, Codes(draft));
    }

    [Fact]
    public void Yen_has_no_minor_units()
    {
        var draft = Normalize("JPY", "2900", ("Ramen", "1000"), ("Gyoza", "1000"), ("Beer", "1000"), ("Coupon", "-100"));

        Assert.Equal([966m, 967m, 967m], Amounts(draft));
        Assert.Equal("100", Assert.Single(draft.Warnings).Params["amount"]);
    }

    [Fact]
    public void Dinar_has_three_minor_units()
    {
        var draft = Normalize("KWD", "2.900", ("A", "1.000"), ("B", "2.000"), ("Coupon", "-0.100"));

        Assert.Equal([0.967m, 1.933m], Amounts(draft));
        Assert.Equal("0.100", Assert.Single(draft.Warnings).Params["amount"]);
    }

    [Fact]
    public void A_discount_as_big_as_all_lines_is_left_out()
    {
        var draft = Normalize("EUR", "0.00", ("A", "1.00"), ("Coupon", "-1.00"));

        Assert.Equal([1.00m], Amounts(draft));
        Assert.Contains(ReceiptWarnings.DiscountSkipped, Codes(draft));
        Assert.Contains(ReceiptWarnings.LinesTotalMismatch, Codes(draft)); // nothing is "fixed"
    }

    [Fact]
    public void Tiny_lines_keep_at_least_one_cent()
    {
        var draft = Normalize("EUR", null, ("Bag", "0.01"), ("TV", "100.00"), ("Coupon", "-50.00"));

        Assert.Equal([0.01m, 50.00m], Amounts(draft));
    }

    [Fact]
    public void Folded_drafts_have_no_negative_lines_and_parse_as_text()
    {
        var draft = Normalize("EUR", "8.25",
            ("Milk", "2.00"), ("Discount", "-0.50"), ("Bread", "3.00"), ("Cheese", "5.00"), ("Loyalty card", "-1.25"));

        Assert.All(draft.Lines, l => Assert.True(l.Subtotal > 0));
        var parsed = new ParserService(null!).ParseBill(Guid.NewGuid(), ReceiptTextBuilder.Build(draft, "@anna"));
        Assert.True(parsed.IsValid, string.Join("; ", parsed.Errors));
        Assert.Equal(8.25m, parsed.Result!.Lines.Sum(l => l.Subtotal));
    }
}
