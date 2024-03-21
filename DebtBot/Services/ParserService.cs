using DebtBot.DB;
using DebtBot.Models.Bill;
using Microsoft.EntityFrameworkCore;
using DebtBot.Models.User;

namespace DebtBot.Services;

public class ParserService : IParserService
{
    private readonly DebtContext _debtContext;

    public ParserService(DebtContext debtContext)
    {
        _debtContext = debtContext;
    }

    public BillParserModel ParseBill(Guid creatorId, string billString)
    {
        var billModel = new BillParserModel
        {
            Date = DateTime.UtcNow
        };

        var sections = billString.Split("\n\n");

        // description
        billModel.Description = sections[0];

        // summary
        var summarySection = sections[1].Split("\n");

        billModel.TotalWithTips = decimal.Parse(summarySection[0]);
        billModel.CurrencyCode = summarySection[1];
         
        billModel.PaymentCurrencyCode = summarySection.Length < 3 ? billModel.CurrencyCode : summarySection[2];
        
        billModel.ChargeInPaymentCurrency = summarySection.Length < 4 
                                            || string.Equals(
                                                 summarySection[3], 
                                                 billModel.CurrencyCode,
                                                 StringComparison.InvariantCultureIgnoreCase);

        // payments
        var paymentsSection = sections[2].Split("\n");
        billModel.Payments = paymentsSection
            .Select(Extensions.Extensions.WhitespaceLocator)
            .Select(q => new BillPaymentParserModel
            {
                Amount = decimal.Parse(q.val.Substring(0, q.index)),
                User = new UserSearchModel { QueryString = q.val.Substring(q.index + 1) }
            })
            .ToList();

        // lines
        var linesSections = sections.Skip(3);
        billModel.Lines = linesSections
            .Select(q => q.Split("\n"))
            .Select(q => new BillLineParserModel
            {
                ItemDescription = q[0],
                Subtotal = decimal.Parse(q[1]),
                Participants = q.Skip(2)
                                .Select(Extensions.Extensions.WhitespaceLocator)
                                .Select(w => new BillLineParticipantParserModel
                                {
                                    Part = decimal.Parse(w.val.Substring(0, w.index)),
                                    User = new UserSearchModel { QueryString = w.val.Substring(w.index + 1) }
                                })
                                .ToList()
            })
            .ToList();

        return billModel;
    }

    private Guid? DetectUser(Guid userId, string strings)
    {
        var user = _debtContext
            .UserContactsLinks
            .Include(u => u.ContactUser)
            .Where(u => u.UserId == userId)
            .Where(u =>
                u.DisplayName == strings
                || u.ContactUser.DisplayName == strings
                || u.ContactUserId.ToString() == strings
                || (u.ContactUser.TelegramId ?? 0).ToString() == strings
                || u.ContactUser.TelegramUserName == strings
                || u.ContactUser.Phone == strings
                || u.ContactUser.Email == strings)
            .Select(u => u.ContactUser)
            .FirstOrDefault();

        user ??= _debtContext
            .Users
            .FirstOrDefault(u =>
                u.Id.ToString() == strings
                || (u.TelegramId ?? 0).ToString() == strings
                || u.TelegramUserName == strings
                || u.Phone == strings
                || u.Email == strings);

        return user?.Id;
    }
}
