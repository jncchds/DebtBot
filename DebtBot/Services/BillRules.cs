using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
using DebtBot.Models.User;

namespace DebtBot.Services;

/// <summary>
/// Validation and mapping for bills created from structured (form) models.
/// The structured models are mapped to the parser models, so they go through the same
/// repository path as bills parsed from text.
/// </summary>
public static class BillRules
{
    public static bool IsCurrencyCode(string? code) =>
        code is { Length: 3 } && code.All(char.IsLetter);

    public static List<string> ValidateBill(BillCreationModel bill)
    {
        var errors = new List<string>();

        if (!IsCurrencyCode(bill.CurrencyCode))
            errors.Add("Currency code must be 3 letters");
        if (bill.PaymentCurrencyCode is not null && !IsCurrencyCode(bill.PaymentCurrencyCode))
            errors.Add("Payment currency code must be 3 letters");
        if (bill.TotalWithTips is not null && bill.TotalWithTips <= 0)
            errors.Add("Total with tips must be above zero");

        errors.AddRange(ValidateLines(bill.Lines ?? []));
        errors.AddRange(ValidatePayments(bill.Payments ?? []));

        return errors;
    }

    public static List<string> ValidateLines(IReadOnlyList<BillLineCreationModel> lines)
    {
        var errors = new List<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var name = $"Line {i + 1}";
            if (line is null)
            {
                errors.Add($"{name} is empty");
                continue;
            }
            if (line.Subtotal <= 0)
                errors.Add($"{name}: subtotal must be above zero");

            var participants = line.Participants ?? [];
            if (participants.Count == 0)
                errors.Add($"{name}: at least one participant is needed");
            if (participants.Any(p => p.UserId == Guid.Empty))
                errors.Add($"{name}: participant user id is missing");
            if (participants.Any(p => p.Part is not null && p.Part <= 0))
                errors.Add($"{name}: parts must be above zero");
            if (participants.GroupBy(p => p.UserId).Any(g => g.Count() > 1))
                errors.Add($"{name}: a user can take part in a line only once");
        }

        return errors;
    }

    public static List<string> ValidatePayments(IReadOnlyList<BillPaymentCreationModel> payments)
    {
        var errors = new List<string>();

        for (var i = 0; i < payments.Count; i++)
        {
            var payment = payments[i];
            var name = $"Payment {i + 1}";
            if (payment is null)
            {
                errors.Add($"{name} is empty");
                continue;
            }
            if (payment.UserId == Guid.Empty)
                errors.Add($"{name}: user id is missing");
            if (payment.Amount <= 0)
                errors.Add($"{name}: amount must be above zero");
        }

        if (payments.Where(p => p is not null).GroupBy(p => p.UserId).Any(g => g.Count() > 1))
            errors.Add("A user can appear in the payments only once");

        return errors;
    }

    /// <summary>Equal split: a participant without a part counts as 1.</summary>
    public static List<BillLineParserModel> ToParserLines(IEnumerable<BillLineCreationModel> lines) =>
        lines.Select(l => new BillLineParserModel
        {
            ItemDescription = l.ItemDescription ?? "",
            Subtotal = l.Subtotal,
            Participants = (l.Participants ?? []).Select(p => new BillLineParticipantParserModel
            {
                Part = p.Part ?? 1m,
                User = new UserSearchModel { Id = p.UserId }
            }).ToList()
        }).ToList();

    public static List<BillPaymentParserModel> ToParserPayments(IEnumerable<BillPaymentCreationModel> payments) =>
        payments.Select(p => new BillPaymentParserModel
        {
            Amount = p.Amount,
            User = new UserSearchModel { Id = p.UserId }
        }).ToList();

    public static BillParserModel ToParserModel(BillCreationModel bill, DateTime now)
    {
        var lines = ToParserLines(bill.Lines ?? []);
        var currency = bill.CurrencyCode.ToUpperInvariant();

        return new BillParserModel
        {
            CurrencyCode = currency,
            PaymentCurrencyCode = bill.PaymentCurrencyCode?.ToUpperInvariant() ?? currency,
            Description = bill.Description ?? "",
            Date = bill.Date ?? now,
            TotalWithTips = bill.TotalWithTips ?? lines.Sum(l => l.Subtotal ?? 0m),
            Lines = lines,
            Payments = ToParserPayments(bill.Payments ?? [])
        };
    }

    /// <summary>The user ids a structured bill refers to.</summary>
    public static IEnumerable<Guid> UserIds(IEnumerable<BillLineCreationModel>? lines, IEnumerable<BillPaymentCreationModel>? payments) =>
        (lines ?? []).SelectMany(l => l.Participants ?? []).Select(p => p.UserId)
            .Concat((payments ?? []).Select(p => p.UserId))
            .Distinct();

    /// <summary>What a draft needs before it can be finalized; otherwise the ledger calculation divides by zero.</summary>
    public static List<string> ValidateForFinalize(BillModel bill)
    {
        var errors = new List<string>();

        if (bill.Status != ProcessingState.Draft)
        {
            errors.Add("Only draft bills can be finalized");
            return errors;
        }

        var lines = bill.Lines ?? [];
        var payments = bill.Payments ?? [];

        if (lines.Count == 0 || lines.Sum(l => l.Subtotal) <= 0)
            errors.Add("The bill needs at least one line with a subtotal above zero");
        if (lines.Any(l => (l.Participants ?? []).Sum(p => p.Part) <= 0))
            errors.Add("Every line needs participants");
        if (payments.Count == 0 || payments.Sum(p => p.Amount) <= 0)
            errors.Add("The bill needs at least one payment above zero");
        if (bill.TotalWithTips <= 0)
            errors.Add("Total with tips must be above zero");

        return errors;
    }
}

public static class ExchangeRules
{
    public static List<string> Validate(ExchangeCreationModel exchange)
    {
        var errors = new List<string>();

        if (exchange.FromUserId == Guid.Empty || exchange.ToUserId == Guid.Empty)
            errors.Add("Both users are needed");
        else if (exchange.FromUserId == exchange.ToUserId)
            errors.Add("An exchange needs two different users");
        if (exchange.FromAmount <= 0 || exchange.ToAmount <= 0)
            errors.Add("Amounts must be above zero");
        if (!BillRules.IsCurrencyCode(exchange.FromCurrencyCode) || !BillRules.IsCurrencyCode(exchange.ToCurrencyCode))
            errors.Add("Currency codes must be 3 letters");

        return errors;
    }

    /// <summary>
    /// The two bills of an exchange, built the same way as Telegram's <c>/ExchangeDebt</c>.
    /// In the forward bill the forward user pays <c>forward.Value forward.CurrencyCode</c> for the backward user's
    /// single "Exchange" line of <c>backward.Value backward.CurrencyCode</c>, and the backward bill mirrors it.
    /// </summary>
    public static (BillParserModel forwardBill, BillParserModel backwardBill) BuildBills(
        ExchangeLineParserModel forward,
        ExchangeLineParserModel backward,
        string? description,
        DateTime date)
    {
        var forwardBill = new BillParserModel { Description = description ?? "" };
        var backwardBill = new BillParserModel { Description = description ?? "" };

        FillBill(forwardBill, forward, backward, date);
        FillBill(backwardBill, backward, forward, date);

        return (forwardBill, backwardBill);
    }

    public static (BillParserModel forwardBill, BillParserModel backwardBill) BuildBills(ExchangeCreationModel exchange, DateTime now) =>
        BuildBills(
            new ExchangeLineParserModel
            {
                AmountType = ExchangeAmountType.Absolute,
                Value = exchange.FromAmount,
                CurrencyCode = exchange.FromCurrencyCode.ToUpperInvariant(),
                User = new UserSearchModel { Id = exchange.FromUserId }
            },
            new ExchangeLineParserModel
            {
                AmountType = ExchangeAmountType.Absolute,
                Value = exchange.ToAmount,
                CurrencyCode = exchange.ToCurrencyCode.ToUpperInvariant(),
                User = new UserSearchModel { Id = exchange.ToUserId }
            },
            string.IsNullOrWhiteSpace(exchange.Description) ? "Exchange" : exchange.Description.Trim(),
            exchange.Date ?? now);

    private static void FillBill(BillParserModel bill, ExchangeLineParserModel forwardLine, ExchangeLineParserModel backwardLine, DateTime date)
    {
        bill.CurrencyCode = backwardLine.CurrencyCode;
        bill.PaymentCurrencyCode = forwardLine.CurrencyCode;

        bill.TotalWithTips = backwardLine.Value;
        bill.Date = date;

        bill.Description += $"\nExchange {forwardLine.Value / backwardLine.Value:0.##} {forwardLine.CurrencyCode}/{backwardLine.CurrencyCode}";

        bill.Lines =
        [
            new BillLineParserModel
            {
                ItemDescription = "Exchange",
                Subtotal = backwardLine.Value,
                Participants =
                [
                    new BillLineParticipantParserModel
                    {
                        Part = 1,
                        User = backwardLine.User
                    }
                ]
            }
        ];

        bill.Payments =
        [
            new BillPaymentParserModel
            {
                Amount = forwardLine.Value,
                User = forwardLine.User
            }
        ];
    }

    /// <summary>
    /// Checks that two bills form an exchange pair that can be accepted. Bills made by <c>/ExchangeDebt</c> before
    /// the pair link existed have no link; they are accepted as long as neither is linked to a third bill.
    /// </summary>
    public static List<string> ValidatePair(BillModel forward, BillModel backward)
    {
        var errors = new List<string>();

        if (forward.Id == backward.Id)
        {
            errors.Add("The two bills of an exchange must be different");
            return errors;
        }

        if ((forward.ExchangeBillId is not null && forward.ExchangeBillId != backward.Id)
            || (backward.ExchangeBillId is not null && backward.ExchangeBillId != forward.Id))
        {
            errors.Add("These bills aren't an exchange pair");
        }

        if (forward.Status != ProcessingState.Draft || backward.Status != ProcessingState.Draft)
            errors.Add("Only a draft exchange can be accepted");

        return errors;
    }
}
