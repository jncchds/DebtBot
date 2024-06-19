using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using DebtBot.Telegram;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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

    public ValidationModel<BillParserModel> ParseBill(string parsedText, List<UserSearchModel> userSearchModels)
    {
        var billModel = new BillParserModel
        {
            Date = DateTime.UtcNow
        };

        decimal decVal;
        string? strVal;
        var returnModel = new ValidationModel<BillParserModel>
        {
            Result = billModel
        };

        if (string.IsNullOrWhiteSpace(parsedText))
        {
            returnModel.Errors.Add("Message is empty");
            return returnModel;
        }

        var sections = parsedText.Split("\n\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (sections.Length < 2)
        {
            returnModel.Errors.Add("Bill must contain the header (amount and currency)");
            return returnModel;
        }

        // description
        billModel.Description = sections[0];

        // summary
        var summarySection = sections[1].Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (summarySection.Length < 2)
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

        if (sections.Length < 3)
            return returnModel;

        // payments
        var payments = ParsePayments(sections[2], userSearchModels);
        if (payments.Errors.Any())
        {
            returnModel.Errors.AddRange(payments.Errors);
            return returnModel;
        }

        billModel.Payments = payments.Result!;

        // lines
        if (sections.Length < 4)
            return returnModel;

        var lines = parseLines(sections.Skip(3), userSearchModels);
        if (lines.Errors.Any())
        {
            returnModel.Errors.AddRange(lines.Errors);
            return returnModel;
        }
        billModel.Lines = lines.Result!;

        return returnModel;
    }

    public ValidationModel<List<BillLineParserModel>> ParseLines(string parsedText, List<UserSearchModel> userSearchModels)
    {
        var sections = parsedText.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parseLines(sections, userSearchModels);
    }
    
    private ValidationModel<List<BillLineParserModel>> parseLines(IEnumerable<string> linesSections, List<UserSearchModel> userSearchModels)
    {
        var returnModel = new ValidationModel<List<BillLineParserModel>>();
        decimal decVal;
        string? strVal;

        var lines = linesSections
            .Select(q => q.Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Select(q =>
            {
                if (q.Length < 2)
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
                                returnModel.Errors.Add($"Failed to parse participant part <pre>{strVal}</pre>");
                                return null;
                            }
                            strVal = w.val.Substring(w.index + 1).Trim();
                            if (string.IsNullOrWhiteSpace(strVal))
                            {
                                returnModel.Errors.Add($"Participant must contain a user");
                                return null;
                            }
                            return new BillLineParticipantParserModel()
                            {
                                Part = decVal,
                                User = findMentionedUser(strVal, userSearchModels)
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

                return new BillLineParserModel()
                {
                    ItemDescription = q[0],
                    Subtotal = decVal,
                    Participants = participants.Select(t => t!).ToList()
                };
            })
            .ToList();

        if (!returnModel.Errors.Any())
            returnModel.Result = lines.Select(t => t!).ToList();

        return returnModel;
    }
    
    public ValidationModel<List<BillPaymentParserModel>> ParsePayments(string parsedText, List<UserSearchModel> userSearchModels)
    {
        var returnModel = new ValidationModel<List<BillPaymentParserModel>>();
        decimal decVal;
        string? strVal;

        var paymentsSection = parsedText.Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
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
                    returnModel.Errors.Add($"Failed to parse payment amount");
                    return null;
                }

                strVal = q.val.Substring(q.index + 1).Trim();
                if (string.IsNullOrWhiteSpace(strVal))
                {
                    returnModel.Errors.Add($"Payment must contain a user");
                    return null;
                }
                
                return new BillPaymentParserModel()
                {
                    Amount = decVal,
                    User = findMentionedUser(strVal, userSearchModels)
                };
            })
            .ToList();

        if (!returnModel.Errors.Any())
            returnModel.Result = payments.Select(t => t!).ToList();

        return returnModel;
    }
}
