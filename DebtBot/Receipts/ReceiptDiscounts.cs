using DebtBot.Models.Receipt;
using System.Text.RegularExpressions;

namespace DebtBot.Receipts;

/// <summary>
/// Folds negative lines (discounts) into the other lines, so a draft never holds a line at or below zero (the form
/// and <c>BillRules</c> refuse them). The lines' sum, and so the receipt's total, never changes.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A discount right under an item is subtracted from that item (<see cref="ReceiptWarnings.DiscountFolded"/>).</item>
/// <item>A discount with no item above it, or one that reads as a whole-receipt discount (loyalty, coupon, card,
/// bonus, a plain "Discount" after the last item…), is spread over all lines in proportion to their amounts,
/// rounded to the currency's minor units, with the rounding remainder on the largest line
/// (<see cref="ReceiptWarnings.DiscountSpread"/>).</item>
/// <item>A discount that would take the item above it to zero or below is spread instead.</item>
/// <item>Discounts as big as all the lines together can't be spread; they're left out
/// (<see cref="ReceiptWarnings.DiscountSkipped"/>), and the total check then reports the mismatch.</item>
/// </list>
/// </remarks>
public static class ReceiptDiscounts
{
    /// <summary>Words that make a discount belong to the whole receipt, not to the item above it.</summary>
    private static readonly Regex WholeReceipt = new(
        @"loyal|coupon|voucher|promo|member|club|card|bonus|point|reward|cashback|gift|sub\s*total|total|order|receipt|bill\b|check\b"
        + @"|лояль|купон|карт|бонус|балл|промо|итог|чек|заказ|сертификат"
        + @"|treue|gutschein|kunden|fid[eé]lit|bon d'achat|fedelt|tessera|buono|cup[oó]n",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>A plain "Discount" (with at most a percentage or a number): an item's or the receipt's, by its place.</summary>
    private static readonly Regex Generic = new(
        @"^\s*(discount|скидка|rabatt|remise|r[eé]duction|sconto|descuento|desconto|korting|rabat|zni[zż]ka|indirim|sleva)[\s\d.,%:*()×x-]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static List<ReceiptDraftLine> Fold(List<ReceiptDraftLine> lines, string? currency, List<ReceiptWarning> warnings)
    {
        if (!lines.Any(l => l.Subtotal < 0))
        {
            return lines;
        }

        var result = new List<ReceiptDraftLine>(lines.Count);
        var toSpread = new List<(string Name, decimal Amount)>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Subtotal >= 0)
            {
                result.Add(new ReceiptDraftLine { Description = line.Description, Subtotal = line.Subtotal });
                continue;
            }

            var amount = -line.Subtotal;
            var item = result.Count > 0 ? result[^1] : null;
            var trailing = lines.Skip(i + 1).All(l => l.Subtotal < 0);
            var whole = item is null
                || WholeReceipt.IsMatch(line.Description)
                || (trailing && result.Count > 1 && Generic.IsMatch(line.Description));

            if (!whole && item!.Subtotal - amount > 0)
            {
                item.Subtotal -= amount;
                warnings.Add(new(ReceiptWarnings.DiscountFolded,
                    ("name", line.Description), ("item", item.Description), ("amount", ReceiptNormalizer.Amount(amount, currency))));
            }
            else
            {
                toSpread.Add((line.Description, amount));
            }
        }

        if (toSpread.Count > 0)
        {
            var total = toSpread.Sum(d => d.Amount);
            if (Spread(result, total, currency))
            {
                foreach (var (name, amount) in toSpread)
                {
                    warnings.Add(new(ReceiptWarnings.DiscountSpread, ("name", name), ("amount", ReceiptNormalizer.Amount(amount, currency))));
                }
            }
            else
            {
                foreach (var (name, amount) in toSpread)
                {
                    warnings.Add(new(ReceiptWarnings.DiscountSkipped, ("name", name), ("amount", ReceiptNormalizer.Amount(amount, currency))));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Takes <paramref name="discount"/> off the lines in proportion to their amounts, keeping every line at one minor
    /// unit or more, with the rounding remainder on the largest line. False (and nothing changed) when it can't.
    /// </summary>
    private static bool Spread(List<ReceiptDraftLine> lines, decimal discount, string? currency)
    {
        var minor = ReceiptNormalizer.MinorUnit(currency);
        var sum = lines.Sum(l => l.Subtotal);
        if (lines.Count == 0 || discount >= sum)
        {
            return false;
        }

        var shares = new decimal[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            var amount = lines[i].Subtotal;
            if (amount <= 0)
            {
                continue;
            }
            var share = Currencies.Round(discount * amount / sum, currency);
            shares[i] = Math.Max(0, Math.Min(share, amount - minor));
        }

        var largest = 0;
        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i].Subtotal > lines[largest].Subtotal)
            {
                largest = i;
            }
        }
        shares[largest] += discount - shares.Sum();
        if (lines[largest].Subtotal - shares[largest] < minor)
        {
            return false;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            lines[i].Subtotal -= shares[i];
        }
        return true;
    }
}
