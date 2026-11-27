using DebtBot.Models.Receipt;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DebtBot.Receipts;

/// <summary>Warning codes in <see cref="ReceiptDraft.Warnings"/>, with their parameters.</summary>
public static class ReceiptWarnings
{
    /// <summary><c>linesSum</c>, <c>total</c> (with tips), <c>difference</c> (total − lines): the lines don't add up to the total within 1%.</summary>
    public const string LinesTotalMismatch = "linesTotalMismatch";
    /// <summary><c>symbol</c>: the sign fits several currencies and the last bill's currency isn't one of them.</summary>
    public const string CurrencyAmbiguous = "currencyAmbiguous";
    /// <summary><c>symbol</c>, <c>currency</c>: an ambiguous sign read as the currency of your last bill.</summary>
    public const string CurrencyFromLastBill = "currencyFromLastBill";
    /// <summary><c>symbol</c> (when one was printed): no currency could be read.</summary>
    public const string CurrencyMissing = "currencyMissing";
    /// <summary>No total printed: the form's total stays empty (the sum of the lines).</summary>
    public const string TotalMissing = "totalMissing";
    /// <summary><c>amount</c>: tips printed outside the total were added to it.</summary>
    public const string TipsAdded = "tipsAdded";
    /// <summary><c>amount</c>: a service charge printed outside the total was added to it.</summary>
    public const string ServiceChargeAdded = "serviceChargeAdded";
    /// <summary><c>line</c> (1-based), <c>amount</c>: a negative line, kept as a discount line. The form needs positive lines, so it has to be spread or removed before saving.</summary>
    public const string DiscountLine = "discountLine";
    /// <summary><c>field</c> (total, tips, serviceCharge, tax): a negative amount outside the lines, ignored.</summary>
    public const string NegativeAmountIgnored = "negativeAmountIgnored";
    /// <summary><c>name</c>: an item without a readable amount, left out.</summary>
    public const string ItemSkipped = "itemSkipped";
    /// <summary><c>count</c>, <c>kept</c>: more items than the limit; the first ones are kept.</summary>
    public const string TooManyItems = "tooManyItems";
    /// <summary><c>line</c>: a name cut to the line description length.</summary>
    public const string NameTruncated = "nameTruncated";
    /// <summary><c>value</c>: the printed date couldn't be read.</summary>
    public const string DateUnreadable = "dateUnreadable";
}

/// <summary>
/// Turns the model's answer into a <see cref="ReceiptDraft"/> (receipt-parse.md, "Normalisation"). Amounts are only
/// parsed and rounded to the currency's minor units, never "fixed".
/// </summary>
public static class ReceiptNormalizer
{
    public const int MaxItems = 100;
    /// <summary>The web form's line name limit.</summary>
    public const int MaxLineDescriptionLength = 200;
    /// <summary>The web form's description limit.</summary>
    public const int MaxDescriptionLength = 1000;
    public const string DefaultDescription = "Receipt";

    /// <summary>Normalises a valid answer (see <see cref="ReceiptSchema.IsValid"/>). <paramref name="lastBillCurrency"/> resolves an ambiguous sign.</summary>
    public static ReceiptDraft Normalize(JsonElement answer, string? lastBillCurrency)
    {
        var warnings = new List<ReceiptWarning>();
        var draft = new ReceiptDraft { Warnings = warnings };

        var currency = ResolveCurrency(String(answer, "currency"), String(answer, "currencySymbol"), lastBillCurrency, warnings);
        draft.CurrencyCode = currency;

        // Lines.
        var items = answer.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array
            ? itemsElement.EnumerateArray().ToList()
            : [];
        var overflow = 0;
        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = OneLine(String(item, "name"));
            if (!TryParseAmount(Raw(item, "amount"), out var amount))
            {
                warnings.Add(new(ReceiptWarnings.ItemSkipped, ("name", name)));
                continue;
            }

            if (TryParseAmount(Raw(item, "quantity"), out var quantity) && quantity > 0 && quantity != 1)
            {
                name = name.Length == 0 ? "" : $"{name} ×{quantity.ToString("0.###", CultureInfo.InvariantCulture)}";
            }

            if (draft.Lines.Count == MaxItems)
            {
                overflow++;
                continue;
            }

            draft.Lines.Add(new ReceiptDraftLine { Description = name, Subtotal = Currencies.Round(amount, currency) });
        }

        if (overflow > 0)
        {
            warnings.Add(new(ReceiptWarnings.TooManyItems, ("count", Int(MaxItems + overflow)), ("kept", Int(MaxItems))));
        }

        for (var i = 0; i < draft.Lines.Count; i++)
        {
            var line = draft.Lines[i];
            if (line.Description.Length == 0)
            {
                line.Description = $"Item {i + 1}";
            }
            else if (line.Description.Length > MaxLineDescriptionLength)
            {
                line.Description = line.Description[..MaxLineDescriptionLength].TrimEnd();
                warnings.Add(new(ReceiptWarnings.NameTruncated, ("line", Int(i + 1))));
            }

            if (line.Subtotal < 0)
            {
                warnings.Add(new(ReceiptWarnings.DiscountLine, ("line", Int(i + 1)), ("amount", Amount(-line.Subtotal, currency))));
            }
        }

        // Totals. Negative totals, tips, service charges and taxes mean nothing here.
        decimal? Field(string field)
        {
            if (!TryParseAmount(Raw(answer, field), out var value))
            {
                return null;
            }
            if (value < 0)
            {
                warnings.Add(new(ReceiptWarnings.NegativeAmountIgnored, ("field", field)));
                return null;
            }
            return Currencies.Round(value, currency);
        }

        var total = Field("total");
        var tips = Field("tips") ?? 0;
        var service = Field("serviceCharge") ?? 0;
        var tax = Field("tax") ?? 0;
        var tipsOutside = Bool(answer, "tipsIncluded") == false;
        var serviceOutside = Bool(answer, "serviceChargeIncluded") == false;
        var taxOutsideItems = Bool(answer, "taxIncluded") == false;

        if (total is null)
        {
            warnings.Add(new(ReceiptWarnings.TotalMissing));
        }
        else
        {
            var totalWithTips = total.Value;
            if (tipsOutside && tips > 0)
            {
                totalWithTips += tips;
                warnings.Add(new(ReceiptWarnings.TipsAdded, ("amount", Amount(tips, currency))));
            }
            if (serviceOutside && service > 0)
            {
                totalWithTips += service;
                warnings.Add(new(ReceiptWarnings.ServiceChargeAdded, ("amount", Amount(service, currency))));
            }
            draft.TotalWithTips = totalWithTips;

            // What the items should add up to: the printed total without the extras printed inside it.
            if (draft.Lines.Count > 0)
            {
                var expectedItems = total.Value
                    - (tipsOutside ? 0 : tips)
                    - (serviceOutside ? 0 : service)
                    - (taxOutsideItems ? tax : 0);
                var linesSum = draft.Lines.Sum(l => l.Subtotal);
                var tolerance = Math.Max(Math.Abs(expectedItems) * 0.01m, MinorUnit(currency));
                if (Math.Abs(linesSum - expectedItems) > tolerance)
                {
                    warnings.Add(new(ReceiptWarnings.LinesTotalMismatch,
                        ("linesSum", Amount(linesSum, currency)),
                        ("total", Amount(totalWithTips, currency)),
                        ("difference", Amount(totalWithTips - linesSum, currency))));
                }
            }
        }

        // Date and description.
        var merchant = OneLine(String(answer, "merchant"));
        if (merchant.Length == 0)
        {
            merchant = DefaultDescription;
        }

        var rawDate = String(answer, "date")?.Trim();
        if (!string.IsNullOrEmpty(rawDate))
        {
            if (TryParseDate(rawDate, out var date, out var hasTime))
            {
                draft.Date = date;
                if (!hasTime)
                {
                    merchant = $"{merchant} {date:yyyy-MM-dd}";
                }
            }
            else
            {
                warnings.Add(new(ReceiptWarnings.DateUnreadable, ("value", rawDate)));
            }
        }

        draft.Description = merchant.Length > MaxDescriptionLength ? merchant[..MaxDescriptionLength].TrimEnd() : merchant;
        return draft;
    }

    /// <summary>
    /// The currency: a valid ISO code, else the printed sign; an ambiguous sign (<c>$</c>, <c>¥</c>, <c>kr</c>) takes
    /// the last bill's currency when it's one of its meanings, else stays empty with a warning.
    /// </summary>
    public static string? ResolveCurrency(string? currency, string? symbol, string? lastBillCurrency, List<ReceiptWarning> warnings)
    {
        var code = currency?.Trim().ToUpperInvariant();
        if (Currencies.IsIsoCode(code))
        {
            return code;
        }

        var sign = !string.IsNullOrWhiteSpace(symbol) ? symbol.Trim() : currency?.Trim();
        var candidates = Currencies.FromSymbol(symbol);
        if (candidates.Count == 0)
        {
            candidates = Currencies.FromSymbol(currency);
            if (candidates.Count > 0)
            {
                sign = currency!.Trim();
            }
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        if (candidates.Count > 1)
        {
            var last = lastBillCurrency?.Trim().ToUpperInvariant();
            if (last is not null && candidates.Contains(last))
            {
                warnings.Add(new(ReceiptWarnings.CurrencyFromLastBill, ("symbol", sign!), ("currency", last)));
                return last;
            }

            warnings.Add(new(ReceiptWarnings.CurrencyAmbiguous, ("symbol", sign!)));
            return null;
        }

        warnings.Add(string.IsNullOrEmpty(sign)
            ? new ReceiptWarning(ReceiptWarnings.CurrencyMissing)
            : new ReceiptWarning(ReceiptWarnings.CurrencyMissing, ("symbol", sign)));
        return null;
    }

    /// <summary>
    /// A printed amount: spaces, signs and letters dropped. With both <c>.</c> and <c>,</c> the last one is the
    /// decimal separator; a single <c>,</c> followed by one or two digits is a decimal comma, other commas group
    /// thousands; several dots group thousands. A leading or trailing minus, or parentheses, make it negative.
    /// </summary>
    public static bool TryParseAmount(string? raw, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var negative = raw.Contains('-') || raw.Contains('−') || (raw.Trim().StartsWith('(') && raw.Trim().EndsWith(')'));
        var builder = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsAsciiDigit(c) || c is '.' or ',')
            {
                builder.Append(c);
            }
        }

        var s = builder.ToString().Trim('.', ',');
        if (!s.Any(char.IsAsciiDigit))
        {
            return false;
        }

        var lastDot = s.LastIndexOf('.');
        var lastComma = s.LastIndexOf(',');
        if (lastDot >= 0 && lastComma >= 0)
        {
            var separator = lastDot > lastComma ? '.' : ',';
            var group = separator == '.' ? ',' : '.';
            s = s.Replace(group.ToString(), "");
            var at = s.LastIndexOf(separator);
            s = s[..at].Replace(separator.ToString(), "") + "." + s[(at + 1)..];
        }
        else if (lastComma >= 0)
        {
            var digitsAfter = s.Length - lastComma - 1;
            s = s.Count(c => c == ',') == 1 && digitsAfter is 1 or 2
                ? s.Replace(',', '.')
                : s.Replace(",", "");
        }
        else if (s.Count(c => c == '.') > 1)
        {
            s = s.Replace(".", "");
        }

        if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        if (negative)
        {
            value = -value;
        }
        return true;
    }

    /// <summary>An ISO date, with or without a time; the time is kept as printed (as UTC), a date alone gets 12:00.</summary>
    public static bool TryParseDate(string raw, out DateTime date, out bool hasTime)
    {
        // As printed: "2026-11-20T19:42", with seconds, a space instead of T, or a date alone. Fractions and
        // offsets are cut off (the time is the receipt's local time either way).
        string[] withTime = ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm"];
        var text = raw.Trim();
        foreach (var candidate in new[] { text, text.Length >= 19 ? text[..19] : null, text.Length >= 16 ? text[..16] : null })
        {
            if (candidate is not null && DateTime.TryParseExact(candidate, withTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                hasTime = true;
                return InRange(ref date);
            }
        }

        hasTime = false;
        if (text.Length >= 10 && DateTime.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            && (text.Length == 10 || !char.IsAsciiDigit(text[10])))
        {
            date = date.AddHours(12);
            return InRange(ref date);
        }

        date = default;
        return false;

        static bool InRange(ref DateTime value)
        {
            value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return value.Year is >= 2000 and <= 2100;
        }
    }

    public static string Amount(decimal value, string? currency) =>
        Currencies.Round(value, currency).ToString("F" + Currencies.MinorUnits(currency), CultureInfo.InvariantCulture);

    private static decimal MinorUnit(string? currency) => Currencies.MinorUnits(currency) switch
    {
        0 => 1m,
        3 => 0.001m,
        _ => 0.01m,
    };

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string OneLine(string? text) =>
        string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>A string as is, a number as its JSON text (never through a float).</summary>
    private static string? Raw(JsonElement element, string name) =>
        !element.TryGetProperty(name, out var value) ? null
        : value.ValueKind == JsonValueKind.String ? value.GetString()
        : value.ValueKind == JsonValueKind.Number ? value.GetRawText()
        : null;

    private static bool? Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
