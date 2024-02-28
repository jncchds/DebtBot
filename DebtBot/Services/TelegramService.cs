using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using DebtBot.Models.User;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Bill;
using DebtBot.Telegram;
using DebtBot.Models.Enums;

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

    private void parseReplyHeader(ProcessedMessage processedMessage, Message? reply, long? botId)
    {
        if (reply is null)
            return;

        if (reply.From!.Id != botId)
            return;

        var position = reply.Text!.IndexOf("\n", StringComparison.InvariantCultureIgnoreCase);
        var firstLine = reply.Text[..position].Split(" ");

        if (firstLine.Length < 2)
            return;

        if (!Enum.TryParse<ObjectType>(firstLine[0], out var outEnumVal))
        {
            processedMessage.ObjectType = null;
            return;
        }
        processedMessage.ObjectType = outEnumVal;

        if (!Guid.TryParse(firstLine[1], out var outGuidVal))
        {
            processedMessage.ObjectId = null;
            return;
        }
        processedMessage.ObjectId = outGuidVal;
    }

    public ProcessedMessage? ProcessMessage(Message telegramMessage, long? botId)
    {
        List<UserSearchModel> mentions = new();
        
        int pmention = 0;
        int entityId = 0;

        var message = telegramMessage.Text;

        if (message is null)
            return null;

        var processedMessage = new ProcessedMessage()
        {
            FromId = telegramMessage.From!.Id,
            ChatId = telegramMessage.Chat.Id
        };

        parseReplyHeader(processedMessage, telegramMessage.ReplyToMessage, botId);

        var entities = telegramMessage.Entities?.ToList() ?? [];
        entities.Sort((a, b) => a.Offset.CompareTo(b.Offset));

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
                    processedMessage.BotCommand = entityText;
                    break;
                case MessageEntityType.TextMention:
                case MessageEntityType.Mention:
                    sb.Append($"{{{entityId}}}");

                    // We use the precedence of Telegram Id in search over the username
                    UserSearchModel user = new UserSearchModel()
                    {
                        TelegramId = entity.User?.Id,
                        TelegramUserName = entity.Type == MessageEntityType.TextMention ? entity.User!.Username : entityText,
                        DisplayName = entityText,
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

        processedMessage.UserSearchModels = mentions;
        processedMessage.ProcessedText = sb.ToString().Trim();
        
        return processedMessage;
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

    public BillParserModel ParseBill(string parsedText, List<UserSearchModel> userSearchModels)
    {
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
        if (sections.Length > 2)
        {
            billModel.Payments = ParsePayments(sections[2], userSearchModels);
        }

        // lines
        if (sections.Length > 3)
        {
            billModel.Lines = parseLines(sections.Skip(3), userSearchModels);
        }

        return billModel;
    }

    public List<BillLineParserModel> ParseLines(string parsedText, List<UserSearchModel> userSearchModels)
    {
        var sections = parsedText.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parseLines(sections, userSearchModels);
    }
    
    private List<BillLineParserModel> parseLines(IEnumerable<string> linesSections, List<UserSearchModel> userSearchModels)
    {
        return linesSections
            .Select(q => q.Split("\n", StringSplitOptions.TrimEntries))
            .Select(q => new BillLineParserModel()
            {
                ItemDescription = q[0],
                Subtotal = decimal.Parse(q[1]),
                Participants = q.Skip(2)
                    .Select(Extensions.Extensions.WhitespaceLocator)
                    .Select(w => new BillLineParticipantParserModel()
                    {
                        Part = decimal.Parse(w.val.Substring(0, w.index)),
                        User = findMentionedUser(w.val.Substring(w.index + 1), userSearchModels)
                    })
                    .ToList()
            })
            .ToList();
    }
    
    public List<BillPaymentParserModel> ParsePayments(string parsedText, List<UserSearchModel> userSearchModels)
    {
        var paymentsSection = parsedText.Split("\n");
        return paymentsSection
            .Select(Extensions.Extensions.WhitespaceLocator)
            .Select(q => new BillPaymentParserModel()
            {
                Amount = decimal.Parse(q.val.Substring(0, q.index)),
                User = findMentionedUser(q.val.Substring(q.index + 1), userSearchModels)
            })
            .ToList();
    }
}
