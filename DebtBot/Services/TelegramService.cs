using System.Text;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using DebtBot.Models.User;
using DebtBot.Interfaces.Services;

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
        var user = _userService.FindUser(Guid.Empty, new UserSearchModel { TelegramId = telegramId });
        return user?.Id;
    }

    public (string processedText, List<UserModel> entities) IncludeMentions(Guid creatorId, string? message, MessageEntity[]? entities)
    {
        List<UserModel> mentions = new();
        int pmention = 0;
        int entityId = 0;

        if (entities is null)
            entities = Array.Empty<MessageEntity>();

        Array.Sort(entities, (a, b) => a.Offset.CompareTo(b.Offset));


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
                    UserSearchModel model = new UserSearchModel()
                    {
                        TelegramId = entity.User?.Id,
                        TelegramUserName = entityText
                    };
            
                    var user = _userService.FindUser(creatorId, model);

                    user ??= _userService.AddUser(new UserCreationModel()
                    {
                        TelegramId = entity.User?.Id,
                        TelegramUserName = entity.Type == MessageEntityType.TextMention ? entity.User!.Username : entityText,
                        DisplayName = entityText
                    });

                    mentions.Add(user!);

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
}
