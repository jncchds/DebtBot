using DebtBot.Models.Receipt;

namespace DebtBot.Receipts;

/// <summary>
/// The one wording of the receipt warnings, used by the bot and sent to the web as <see cref="ReceiptWarning.Message"/>.
/// Amounts get the currency code when it's known.
/// </summary>
public static class ReceiptWarningText
{
    public static string Describe(ReceiptWarning warning, string? currency)
    {
        string P(string key) => warning.Params.TryGetValue(key, out var value) ? value : "";
        string Money(string key) => string.IsNullOrEmpty(currency) ? P(key) : $"{P(key)} {currency}";

        return warning.Code switch
        {
            ReceiptWarnings.LinesTotalMismatch => $"The lines add up to {Money("linesSum")}, but the total is {Money("total")} (off by {Money("difference")}).",
            ReceiptWarnings.CurrencyAmbiguous => $"“{P("symbol")}” could be several currencies: set the currency.",
            ReceiptWarnings.CurrencyFromLastBill => $"“{P("symbol")}” read as {P("currency")}, the currency of your last bill.",
            ReceiptWarnings.CurrencyMissing => warning.Params.ContainsKey("symbol")
                ? $"The currency “{P("symbol")}” couldn't be read: set the currency."
                : "No currency is printed: set the currency.",
            ReceiptWarnings.TotalMissing => "No total is printed: the total is the sum of the lines.",
            ReceiptWarnings.TipsAdded => $"Tips of {Money("amount")} printed outside the total were added to it.",
            ReceiptWarnings.ServiceChargeAdded => $"A service charge of {Money("amount")} printed outside the total was added to it.",
            ReceiptWarnings.DiscountFolded => $"The discount “{P("name")}” ({Money("amount")}) was taken off “{P("item")}”.",
            ReceiptWarnings.DiscountSpread => $"The discount “{P("name")}” ({Money("amount")}) was spread over all lines.",
            ReceiptWarnings.DiscountSkipped => $"The discount “{P("name")}” ({Money("amount")}) is as big as all the lines and was left out.",
            ReceiptWarnings.NegativeAmountIgnored => $"A negative {FieldName(P("field"))} was ignored.",
            ReceiptWarnings.ItemSkipped => string.IsNullOrEmpty(P("name"))
                ? "An item without a readable amount was left out."
                : $"“{P("name")}” has no readable amount and was left out.",
            ReceiptWarnings.TooManyItems => $"Only the first {P("kept")} of {P("count")} items were kept.",
            ReceiptWarnings.NameTruncated => $"The name of line {P("line")} was shortened.",
            ReceiptWarnings.DateUnreadable => $"The date “{P("value")}” couldn't be read.",
            _ => warning.Code,
        };
    }

    private static string FieldName(string field) => field switch
    {
        "serviceCharge" => "service charge",
        "" => "amount",
        _ => field,
    };
}
