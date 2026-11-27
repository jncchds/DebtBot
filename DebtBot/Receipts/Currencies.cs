namespace DebtBot.Receipts;

/// <summary>ISO 4217 codes, their minor units, and the currency signs printed on receipts.</summary>
public static class Currencies
{
    /// <summary>Active ISO 4217 codes (2026), without funds, metals and testing codes.</summary>
    private static readonly HashSet<string> Codes = new(
        """
        AED AFN ALL AMD ANG AOA ARS AUD AWG AZN BAM BBD BDT BGN BHD BIF BMD BND BOB BRL BSD BTN BWP BYN BZD CAD CDF CHF
        CLP CNY COP CRC CUP CVE CZK DJF DKK DOP DZD EGP ERN ETB EUR FJD FKP GBP GEL GHS GIP GMD GNF GTQ GYD HKD HNL HTG
        HUF IDR ILS INR IQD IRR ISK JMD JOD JPY KES KGS KHR KMF KPW KRW KWD KYD KZT LAK LBP LKR LRD LSL LYD MAD MDL MGA
        MKD MMK MNT MOP MRU MUR MVR MWK MXN MYR MZN NAD NGN NIO NOK NPR NZD OMR PAB PEN PGK PHP PKR PLN PYG QAR RON RSD
        RUB RWF SAR SBD SCR SDG SEK SGD SHP SLE SOS SRD SSP STN SVC SYP SZL THB TJS TMT TND TOP TRY TTD TWD TZS UAH UGX
        USD UYU UZS VES VND VUV WST XAF XCD XCG XOF XPF YER ZAR ZMW ZWG
        """.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries),
        StringComparer.Ordinal);

    private static readonly HashSet<string> NoMinorUnits = new(
        ["BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> ThreeMinorUnits = new(
        ["BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND"],
        StringComparer.Ordinal);

    /// <summary>Currencies written with a plain <c>$</c>.</summary>
    public static readonly string[] Dollars =
        ["USD", "CAD", "AUD", "NZD", "HKD", "SGD", "TWD", "MXN", "ARS", "CLP", "COP", "UYU", "BSD", "BBD", "BZD", "BMD",
         "BND", "FJD", "GYD", "JMD", "KYD", "LRD", "NAD", "SBD", "SRD", "TTD", "XCD"];

    /// <summary>
    /// Signs and abbreviations, lower case, each with the currencies it may mean. More than one means ambiguous.
    /// </summary>
    private static readonly Dictionary<string, string[]> Symbols = new(StringComparer.Ordinal)
    {
        ["$"] = Dollars,
        ["us$"] = ["USD"],
        ["usd$"] = ["USD"],
        ["c$"] = ["CAD"],
        ["ca$"] = ["CAD"],
        ["a$"] = ["AUD"],
        ["au$"] = ["AUD"],
        ["nz$"] = ["NZD"],
        ["hk$"] = ["HKD"],
        ["s$"] = ["SGD"],
        ["nt$"] = ["TWD"],
        ["mx$"] = ["MXN"],
        ["r$"] = ["BRL"],
        ["€"] = ["EUR"],
        ["eur"] = ["EUR"],
        ["euro"] = ["EUR"],
        ["£"] = ["GBP"],
        ["₽"] = ["RUB"],
        ["руб"] = ["RUB"],
        ["руб."] = ["RUB"],
        ["р."] = ["RUB"],
        ["₸"] = ["KZT"],
        ["тг"] = ["KZT"],
        ["₺"] = ["TRY"],
        ["tl"] = ["TRY"],
        ["₴"] = ["UAH"],
        ["грн"] = ["UAH"],
        ["₹"] = ["INR"],
        ["₩"] = ["KRW"],
        ["₪"] = ["ILS"],
        ["₾"] = ["GEL"],
        ["֏"] = ["AMD"],
        ["₼"] = ["AZN"],
        ["zł"] = ["PLN"],
        ["zl"] = ["PLN"],
        ["kč"] = ["CZK"],
        ["ft"] = ["HUF"],
        ["₫"] = ["VND"],
        ["฿"] = ["THB"],
        ["₱"] = ["PHP"],
        ["лв"] = ["BGN"],
        ["лв."] = ["BGN"],
        ["lei"] = ["RON"],
        ["дин"] = ["RSD"],
        ["din"] = ["RSD"],
        ["₮"] = ["MNT"],
        ["сом"] = ["KGS"],
        ["сўм"] = ["UZS"],
        ["円"] = ["JPY"],
        ["元"] = ["CNY"],
        ["¥"] = ["JPY", "CNY"],
        ["kr"] = ["SEK", "NOK", "DKK", "ISK"],
        ["kr."] = ["SEK", "NOK", "DKK", "ISK"],
        ["fr"] = ["CHF"],
        ["fr."] = ["CHF"],
    };

    public static bool IsIsoCode(string? code) => code is not null && Codes.Contains(code);

    /// <summary>Digits after the decimal point: 0, 2 or 3. Unknown codes get 2.</summary>
    public static int MinorUnits(string? code) =>
        code is null ? 2 : NoMinorUnits.Contains(code) ? 0 : ThreeMinorUnits.Contains(code) ? 3 : 2;

    public static decimal Round(decimal value, string? code) =>
        Math.Round(value, MinorUnits(code), MidpointRounding.AwayFromZero);

    /// <summary>The currencies a sign may mean (empty when unknown).</summary>
    public static IReadOnlyList<string> FromSymbol(string? symbol)
    {
        var key = symbol?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(key) ? [] : Symbols.GetValueOrDefault(key) ?? [];
    }
}
