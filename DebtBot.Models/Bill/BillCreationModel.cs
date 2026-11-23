using DebtBot.Models.BillLine;

namespace DebtBot.Models.Bill;

/// <summary>
/// A new bill from the form. It is always created as a draft; finalize it with <c>POST Bills/{id}/Finalize</c>.
/// </summary>
public class BillCreationModel
{
    /// <summary>Bill currency, 3 letters (stored upper case).</summary>
    public string CurrencyCode { get; set; }
    /// <summary>Currency the bill was paid in, 3 letters. Defaults to <see cref="CurrencyCode"/>.</summary>
    public string? PaymentCurrencyCode { get; set; }
    public string? Description { get; set; }
    /// <summary>Defaults to now (UTC).</summary>
    public DateTime? Date { get; set; }
    /// <summary>Total with tips in the bill currency. Defaults to the sum of the line subtotals.</summary>
    public decimal? TotalWithTips { get; set; }

    public List<BillLineCreationModel>? Lines { get; set; }
    public List<BillPaymentCreationModel>? Payments { get; set; }
}
