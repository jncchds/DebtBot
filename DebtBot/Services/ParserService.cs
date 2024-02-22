using DebtBot.DB;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;
using DebtBot.Models;
using Microsoft.EntityFrameworkCore;
using DebtBot.Models.User;
using System.Text.RegularExpressions;
using DebtBot.Interfaces.Services;

namespace DebtBot.Services;

public class ParserService : IParserService
{
    private readonly DebtContext _debtContext;
    private readonly IUserService _userService;

    public ParserService(DebtContext debtContext, IUserService userService)
    {
        _debtContext = debtContext;
        _userService = userService;
    }

    public BillCreationModel ParseBill(Guid creatorId, string billString)
    {
        var billModel = new BillCreationModel()
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
        if (summarySection.Length > 2)
        {
            billModel.PaymentCurrencyCode = summarySection[2];
        }
        else
        {
            billModel.PaymentCurrencyCode = billModel.CurrencyCode;
        }

        // payments
        var paymentsSection = sections[2].Split("\n");
        billModel.Payments = paymentsSection
            .Select(Extensions.Extensions.WhitespaceLocator)
            .Select(q => new BillPaymentModel
            {
                Amount = decimal.Parse(q.val.Substring(0, q.index)),
                UserId = DetectUser(creatorId, q.val.Substring(q.index + 1)) ?? Guid.Empty
            })
            .ToList();

        // lines
        var linesSections = sections.Skip(3);
        billModel.Lines = linesSections
            .Select(q => q.Split("\n"))
            .Select(q => new BillLineCreationModel
            {
                ItemDescription = q[0],
                Subtotal = decimal.Parse(q[1]),
                Participants = q.Skip(2)
                                .Select(Extensions.Extensions.WhitespaceLocator)
                                .Select(w => new BillLineParticipantCreationModel
                                {
                                    Part = decimal.Parse(w.val.Substring(0, w.index)),
                                    UserId = DetectUser(creatorId, w.val.Substring(w.index + 1)) ?? Guid.Empty
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
