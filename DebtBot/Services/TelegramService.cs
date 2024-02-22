using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using DebtBot.Models.User;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Bill;

namespace DebtBot.Services;

public class TelegramService : ITelegramService
{
    private readonly IUserService _userService;

    public TelegramService(IUserService userService)
    {
        _userService = userService;
    }

    public Guid? GetUserByTelegramId(long telegramId)
    {
        var user = _userService.FindUser(new UserSearchModel { TelegramId = telegramId });
        return user?.Id;
    }

    public (string processedText, List<UserSearchModel> entities) IncludeMentions(string? message, List<MessageEntity> entities)
    {
        List<UserSearchModel> mentions = new();
        
        int pmention = 0;
        int entityId = 0;

        entities ??= [];
        entities.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        if (message is null)
            return (string.Empty, mentions);

        var sb = new StringBuilder(message.Length);

        foreach (var entity in entities)
        {
            if (pmention < entity.Offset)
            {
                sb.Append(message.Substring(pmention, entity.Offset - pmention));
            }

            var entityText = message.Substring(entity.Offset, entity.Length);

            switch (entity.Type)
            {
                case MessageEntityType.BotCommand:
                    // Ignoring bot command
                    break;
                case MessageEntityType.TextMention:
                case MessageEntityType.Mention:
                    sb.Append($"{{{entityId}}}");

                    // We use the precedence of Telegram Id in search over the username
                    UserSearchModel user = new UserSearchModel()
                    {
                        TelegramId = entity.User?.Id,
                        TelegramUserName = entity.Type == MessageEntityType.TextMention ? entity.User!.Username : entityText,
                    };

                    mentions.Add(user);

                    entityId++;

                    break;
                default: 
                    sb.Append(entityText);
                    break;
            }

            pmention = entity.Offset + entity.Length;
        }

        if (pmention < message.Length)
        {
            sb.Append(message.Substring(pmention));
        }

        return (sb.ToString().Trim(), mentions);
    }

    public void Actualize(User user)
    {
        _userService.ActualizeTelegramUser(user.Id, user.Username, user.FirstName, user.LastName);
    }

    private UserSearchModel findMentionedUser(string value, List<UserSearchModel>mentions)
    {
        try
        {
            var regex = new Regex("{([0-9]+)}");
            var found = regex.Match(value).Groups[1].Value;
            var parsed = int.Parse(found);
            return mentions[parsed];
        }
        catch (Exception)
        {
            return new UserSearchModel
            {
                QueryString = value
            };
        }
    }

    public BillParserModel ParseBill(string? message, List<MessageEntity> entities)
    {
        (var parsedText, var mentions) = IncludeMentions(message, entities);
        
        var billModel = new BillParserModel
        {
            Date = DateTime.UtcNow
        };

        var sections = parsedText.Split("\n\n");

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
            .Select(q => new BillPaymentParserModel()
            {
                Amount = decimal.Parse(q.val.Substring(0, q.index)),
                User = findMentionedUser(q.val.Substring(q.index + 1), mentions)
            })
            .ToList();

        // lines
        var linesSections = sections.Skip(3);
        billModel.Lines = linesSections
            .Select(q => q.Split("\n"))
            .Select(q => new BillLineParserModel()
            {
                ItemDescription = q[0],
                Subtotal = decimal.Parse(q[1]),
                Participants = q.Skip(2)
                    .Select(Extensions.Extensions.WhitespaceLocator)
                    .Select(w => new BillLineParticipantParserModel()
                    {
                        Part = decimal.Parse(w.val.Substring(0, w.index)),
                        User = findMentionedUser(w.val.Substring(w.index + 1), mentions)
                    })
                    .ToList()
            })
            .ToList();

        return billModel;
    }
}
