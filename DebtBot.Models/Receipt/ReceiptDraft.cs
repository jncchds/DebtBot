namespace DebtBot.Models.Receipt;

/// <summary>
/// A receipt read by the model and normalised: the New bill form's own fields, plus the same bill in the bill text
/// format. Kept 15 minutes for its owner (<c>GET Bills/receipt/{id}</c>). Never holds the image or the raw output.
/// </summary>
public class ReceiptDraft
{
    public Guid Id { get; set; }
    /// <summary>The merchant, plus the date when the receipt prints no time. Never empty.</summary>
    public string Description { get; set; } = "";
    /// <summary>The printed date (UTC as printed; 12:00 when only a date is printed), or null.</summary>
    public DateTime? Date { get; set; }
    /// <summary>ISO 4217 code, or null when unknown or ambiguous (see the warnings).</summary>
    public string? CurrencyCode { get; set; }
    /// <summary>The printed total plus tips and service charge shown outside it; null when no total is printed.</summary>
    public decimal? TotalWithTips { get; set; }
    public List<ReceiptDraftLine> Lines { get; set; } = [];
    public List<ReceiptWarning> Warnings { get; set; } = [];
    /// <summary>The model that answered.</summary>
    public string Model { get; set; } = "";
    /// <summary>The bill in the bill text format, with the sender as the stand-in payer and line participant.</summary>
    public string Text { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class ReceiptDraftLine
{
    public string Description { get; set; } = "";
    public decimal Subtotal { get; set; }
}

/// <summary>A warning for the UI to word: a code plus its parameters (amounts as invariant strings).</summary>
public class ReceiptWarning
{
    public string Code { get; set; } = "";
    public Dictionary<string, string> Params { get; set; } = [];

    public ReceiptWarning()
    {
    }

    public ReceiptWarning(string code, params (string Key, string Value)[] parameters)
    {
        Code = code;
        Params = parameters.ToDictionary(p => p.Key, p => p.Value);
    }
}
