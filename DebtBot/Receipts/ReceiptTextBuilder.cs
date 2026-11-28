using DebtBot.Models.Receipt;
using System.Globalization;

namespace DebtBot.Receipts;

/// <summary>
/// The bill text format (as typed to the bot or on Paste text) for a <see cref="ReceiptDraft"/>. A receipt has no
/// people, so the sender stands in: one payment of the total, and each line shared as <c>1 &lt;sender&gt;</c>.
/// </summary>
public static class ReceiptTextBuilder
{
    /// <summary>The ISO 4217 "no currency" code, written when the receipt's currency is unknown.</summary>
    public const string UnknownCurrency = "XXX";

    public static string Build(ReceiptDraft draft, string sender)
    {
        var currency = draft.CurrencyCode ?? UnknownCurrency;
        var total = draft.TotalWithTips ?? draft.Lines.Sum(l => l.Subtotal);

        var sections = new List<string>
        {
            Clean(draft.Description) is { Length: > 0 } description ? description : ReceiptNormalizer.DefaultDescription,
            $"{Number(total, currency)}\n{currency}",
            $"{Number(total, currency)} {sender}",
        };
        sections.AddRange(draft.Lines.Select(line =>
            $"{(Clean(line.Description) is { Length: > 0 } name ? name : "Item")}\n{Number(line.Subtotal, currency)}\n1 {sender}"));

        return string.Join("\n\n", sections);
    }

    /// <summary>
    /// The stand-in for the sender: their Telegram username as stored (usually <c>@name</c>; the parser matches the
    /// stored value), else their Telegram id. Every user has one of them; the user id is a last resort.
    /// </summary>
    public static string Sender(string? telegramUserName, long? telegramId, Guid userId)
    {
        if (!string.IsNullOrWhiteSpace(telegramUserName) && !telegramUserName.Any(char.IsWhiteSpace))
        {
            return telegramUserName;
        }

        return telegramId?.ToString(CultureInfo.InvariantCulture) ?? userId.ToString();
    }

    private static string Number(decimal value, string currency) =>
        Currencies.Round(value, currency).ToString("F" + Currencies.MinorUnits(currency), CultureInfo.InvariantCulture);

    /// <summary>One line: a description must not break the format's line and section structure.</summary>
    private static string Clean(string? text) =>
        string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
