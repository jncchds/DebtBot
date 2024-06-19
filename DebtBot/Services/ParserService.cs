using DebtBot.DB;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.User;

namespace DebtBot.Services;

public class ParserService : IParserService
{
    private readonly DebtContext _debtContext;

    public ParserService(DebtContext debtContext)
    {
        _debtContext = debtContext;
    }

    public ValidationModel<BillParserModel> ParseBill(Guid creatorId, string billString)
    {
        var billModel = new BillParserModel()
        {
            Date = DateTime.UtcNow
        };

        var returnModel = new ValidationModel<BillParserModel>
        {
            Result = billModel
        };

        if (string.IsNullOrWhiteSpace(billString))
        {
            returnModel.Errors.Add("Message is empty");
            return returnModel;
        }

        var sections = billString.Split("\n\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (sections.Length<2)
        {
            returnModel.Errors.Add("Bill must contain the header (amount and currency)");
            return returnModel;
        }

        // description
        billModel.Description = sections[0];

        // summary
        var summarySection = sections[1].Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        decimal decVal;
        string? strVal;

        if (summarySection.Length< 2)
        {
            returnModel.Errors.Add("Bill must contain bill currency");
            return returnModel;
        }

        if (!decimal.TryParse(summarySection[0], out decVal))
        {
            returnModel.Errors.Add("Failed to parse bill total with tips");
            return returnModel;
        }

        billModel.TotalWithTips = decVal;

        strVal = summarySection[1]?.ToUpper();
        if (strVal?.Length != 3)
        {
            returnModel.Errors.Add("Bill currency code must be 3 characters long");
            return returnModel;
        }
        billModel.CurrencyCode = strVal;

        if (summarySection.Length > 2)
        {
            strVal = summarySection[2]?.ToUpper();
            if (strVal?.Length != 3)
            {
                returnModel.Errors.Add("Bill payment currency code must be 3 characters long");
                return returnModel;
            }
            billModel.PaymentCurrencyCode = strVal;
        }
        else
        {
            billModel.PaymentCurrencyCode = billModel.CurrencyCode;
        }

        if (sections.Length<3)
            return returnModel;

        // payments
        var paymentsSection = sections[2].Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var payments = paymentsSection
            .Select(Extensions.Extensions.WhitespaceLocator)
            .Select(q =>
            {
                if (q.index == -1)
                {
                    returnModel.Errors.Add("Payment must contain an amount and a user, separated with a space");
                    return null;
                }
                
                strVal = q.val.Substring(0, q.index);
                if (!decimal.TryParse(strVal, out decVal))
                {
                    returnModel.Errors.Add($"Failed to parse payment amount <pre>{q.val}</pre>");
                    return null;
                }
                
                strVal = q.val.Substring(q.index + 1).Trim();
                if (string.IsNullOrWhiteSpace(strVal))
                {
                    returnModel.Errors.Add($"Payment must contain a user <pre>{q.val}</pre>");
                    return null;
                }
                
                return new BillPaymentParserModel()
                {
                    Amount = decVal,
                    User = new UserSearchModel { QueryString = strVal }
                };
            })
            .ToList();

        if (returnModel.Errors.Any())
            return returnModel;

        billModel.Payments = payments.Select(t => t!).ToList();

        if (sections.Length < 4)
            return returnModel;

        // lines
        var linesSections = sections.Skip(3);
        var lines = linesSections
            .Select(q => q.Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Select(q =>
            {
                if (q.Length<2)
                {
                    returnModel.Errors.Add("Line must contain description and subtotal");
                    return null;
                }
                var participants = q.Skip(2)
                                    .Select(Extensions.Extensions.WhitespaceLocator)
                                    .Select(w =>
                                    {
                                        if (w.index == -1)
                                        {
                                            returnModel.Errors.Add("Line participation must contain a part and a user separated with a space");
                                            return null;
                                        }
                                        strVal = w.val.Substring(0, w.index);
                                        if (!decimal.TryParse(strVal, out decVal))
                                        {
                                            returnModel.Errors.Add($"Failed to parse participant part <pre>{w.val}</pre>");
                                            return null;
                                        }
                                        strVal = w.val.Substring(w.index + 1).Trim();
                                        if (string.IsNullOrWhiteSpace(strVal))
                                        {
                                            returnModel.Errors.Add($"Participant must contain a user <pre>{w.val}</pre>");
                                            return null;
                                        }
                                        return new BillLineParticipantParserModel
                                        {
                                            Part = decVal,
                                            User = new UserSearchModel { QueryString = strVal }
                                        };
                                    })
                                    .ToList();
                if (returnModel.Errors.Any())
                    return null;

                if (!decimal.TryParse(q[1], out decVal))
                {
                    returnModel.Errors.Add($"Failed to parse line subtotal <pre>{q[1]}</pre>");
                    return null;
                }

                return new BillLineParserModel
                {
                    ItemDescription = q[0],
                    Subtotal = decVal,
                    Participants = participants.Select(t => t!).ToList()
                };
            })
            .ToList();

        if (!returnModel.Errors.Any())
            billModel.Lines = lines.Select(t => t!).ToList();

        return returnModel;
    }
}
