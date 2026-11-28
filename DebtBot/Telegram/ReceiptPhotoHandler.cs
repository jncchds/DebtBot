using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Models.Receipt;
using DebtBot.Models.User;
using DebtBot.Receipts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram;

/// <summary>
/// A receipt photo sent to the bot (receipt-parse.md, "The bot"): in a private chat any photo, or an image sent as a
/// document; in a group only when the caption mentions the bot. It goes through the same <see cref="IReceiptService"/>
/// as the web (limits, errors, drafts), and the reply is the bill in the text format, ready to send back.
/// </summary>
public class ReceiptPhotoHandler
{
    public const string WebFormButton = "Open in the web form";
    public const string PeopleHint = "People and payers are you for now; change them before sending.";
    public const string StartFirst = "Start the bot first: open a private chat with the bot and send /start, then send the photo again.";
    /// <summary>Telegram's limit for a message's text.</summary>
    public const int MaxMessageLength = 4096;

    private readonly IReceiptService _receipts;
    private readonly ReceiptFeature _feature;
    private readonly IUserService _users;
    private readonly DebtContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly TelegramBotInfoProvider _botInfo;
    private readonly IOptions<DebtBotConfiguration> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<ReceiptPhotoHandler> _logger;

    public ReceiptPhotoHandler(IReceiptService receipts, ReceiptFeature feature, IUserService users, DebtContext db,
        IPublishEndpoint publish, TelegramBotInfoProvider botInfo, IOptions<DebtBotConfiguration> options,
        TimeProvider time, ILogger<ReceiptPhotoHandler> logger)
    {
        _receipts = receipts;
        _feature = feature;
        _users = users;
        _db = db;
        _publish = publish;
        _botInfo = botInfo;
        _options = options;
        _time = time;
        _logger = logger;
    }

    /// <summary>A photo, or a document that says it's an image. Anything else isn't ours.</summary>
    public static bool HasImage(Message message) =>
        message.Photo is { Length: > 0 }
        || message.Document?.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Handles the message when it's a receipt photo for the bot; false when it isn't (no image, or a group photo
    /// that doesn't mention the bot), so nothing was sent to the model or the chat.
    /// </summary>
    public async Task<bool> HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (!HasImage(message) || message.From is null)
        {
            return false;
        }
        if (message.EditDate is not null)
        {
            // An edited caption isn't a new photo: don't read (and pay for) it again.
            return true;
        }

        var isPrivate = message.Chat.Type == ChatType.Private;
        if (!isPrivate && !await MentionsBotAsync(botClient, message, cancellationToken))
        {
            return false;
        }

        if (!_receipts.IsEnabled)
        {
            if (isPrivate)
            {
                await ReplyAsync(message, ReceiptErrors.Message(ReceiptError.Unavailable));
            }
            return true;
        }

        var user = _users.FindUser(new UserSearchModel { TelegramId = message.From.Id });
        if (user is null || !user.TelegramBotEnabled)
        {
            await ReplyAsync(message, StartFirst);
            return true;
        }

        var file = Largest(message);
        var maxBytes = _options.Value.Llm.MaxImageBytes;
        if (file.Size > maxBytes)
        {
            await ReplyAsync(message, ReceiptErrors.Message(ReceiptError.TooLarge));
            return true;
        }

        byte[]? image;
        try
        {
            image = await DownloadAsync(botClient, file.Id, maxBytes, cancellationToken);
            if (image is null)
            {
                await ReplyAsync(message, ReceiptErrors.Message(ReceiptError.TooLarge));
                return true;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning("Receipt photo download failed: {Error}", e.GetType().Name);
            await ReplyAsync(message, "The photo couldn't be downloaded from Telegram. Try sending it again.");
            return true;
        }

        // The file name (or the caption) is only a hint for the fake client.
        var result = await _receipts.ParseAsync(user.Id, new ReceiptInput(image, file.Name ?? message.Caption), cancellationToken);

        if (result.Draft is { } draft)
        {
            var note = await PrivacyLineAsync(message.Chat.Id, cancellationToken);
            await ReplyAsync(message, BuildReply(draft, note), WebFormButtons(draft.Id));
            return true;
        }

        var error = result.Error ?? ReceiptError.Upstream;
        var text = Escape(ErrorText(error, result.Limit));
        if (ReachedModel(error))
        {
            text += "\n\n" + await PrivacyLineAsync(message.Chat.Id, cancellationToken);
        }
        await ReplyAsync(message, text);
        return true;
    }

    /// <summary>The reply for a read receipt (HTML): the bill as a code block to copy, the hint, the warnings and the note.</summary>
    public static string BuildReply(ReceiptDraft draft, string privacyLine)
    {
        var tail = new StringBuilder();
        tail.Append('\n').Append(Escape(PeopleHint));
        var warnings = WarningLines(draft.Warnings);
        if (warnings.Length > 0)
        {
            tail.Append("\n\n").Append(Escape(warnings));
        }
        tail.Append("\n\n").Append(privacyLine);

        var bill = $"<pre>{Escape(BillText(draft))}</pre>";
        if (bill.Length + tail.Length > MaxMessageLength)
        {
            bill = Escape("The bill is too long for one Telegram message. Open it in the web form, or split it into several bills.");
        }
        return bill + tail;
    }

    /// <summary>The message to send back to make the bill: the typed-bill command and the draft's text.</summary>
    public static string BillText(ReceiptDraft draft) => $"{Commands.Message.AddBillCommand.CommandString}\n{draft.Text.TrimEnd()}";

    /// <summary>At most two short lines: the first warning, and the second with how many more there are.</summary>
    public static string WarningLines(IReadOnlyList<ReceiptWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            return "";
        }
        var lines = warnings.Take(2).Select(w => "⚠️ " + (string.IsNullOrEmpty(w.Message) ? w.Code : w.Message)).ToList();
        if (warnings.Count > 2)
        {
            lines[1] += $" (+{warnings.Count - 2} more)";
        }
        return string.Join('\n', lines);
    }

    /// <summary>The error's message, the same as the web's; a reached limit says when to try again.</summary>
    public static string ErrorText(ReceiptError error, ReceiptLimitDecision? limit)
    {
        var message = ReceiptErrors.Message(error);
        if (error == ReceiptError.RateLimited && limit?.RetryAfter is TimeSpan retryAfter)
        {
            message += $" You can read the next one in {Duration(retryAfter)}.";
        }
        return message;
    }

    /// <summary>"1 minute", "42 minutes", "3 hours", rounded up.</summary>
    public static string Duration(TimeSpan span)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(span.TotalMinutes));
        if (minutes < 60)
        {
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }
        var hours = (int)Math.Ceiling(minutes / 60.0);
        return hours == 1 ? "1 hour" : $"{hours} hours";
    }

    /// <summary>The privacy note the first time a photo is read in this chat, then a short footer.</summary>
    private async Task<string> PrivacyLineAsync(long chatId, CancellationToken cancellationToken)
    {
        var state = await _db.TelegramChatStates.FirstOrDefaultAsync(s => s.ChatId == chatId, cancellationToken);
        if (state?.ReceiptNoteShownAt is not null)
        {
            return $"<i>{Escape(PrivacyFooter(_feature.Model))}</i>";
        }

        if (state is null)
        {
            state = new TelegramChatState { ChatId = chatId };
            _db.TelegramChatStates.Add(state);
        }
        state.ReceiptNoteShownAt = _time.GetUtcNow().UtcDateTime;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another photo in the same chat got there first: it shows the note.
            _db.ChangeTracker.Clear();
            return $"<i>{Escape(PrivacyFooter(_feature.Model))}</i>";
        }
        return $"<i>{Escape(PrivacyNote(_feature.Model))}</i>";
    }

    public static string PrivacyNote(string model) =>
        $"The receipt is sent to an AI service (OpenRouter, model {model}) to read it. Don't send receipts with personal data you don't want to share.";

    public static string PrivacyFooter(string model) => $"Read by an AI service (OpenRouter, {model}).";

    /// <summary>The errors that come after the photo was sent to the model (so the privacy line belongs to them).</summary>
    private static bool ReachedModel(ReceiptError error) =>
        error is ReceiptError.NotAReceipt or ReceiptError.Unreadable or ReceiptError.Busy or ReceiptError.Timeout or ReceiptError.Upstream;

    /// <summary>The "Open in the web form" button, or none when <c>WebUrl</c> is empty or not a link Telegram takes.</summary>
    private List<List<InlineButtonRecord>>? WebFormButtons(Guid draftId)
    {
        var url = WebFormUrl(_options.Value.WebUrl, draftId);
        return url is null ? null : [[new InlineButtonRecord(WebFormButton, "", url)]];
    }

    /// <summary>
    /// <c>{WebUrl}/bills/new?receipt={id}</c>, or null when <paramref name="webUrl"/> is empty or a link Telegram
    /// refuses in a button (not http(s), localhost, a bare host name). The sender also retries without it.
    /// </summary>
    public static string? WebFormUrl(string? webUrl, Guid draftId)
    {
        if (string.IsNullOrWhiteSpace(webUrl)
            || !Uri.TryCreate(webUrl.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.IsLoopback
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (uri.HostNameType == UriHostNameType.Dns && !uri.Host.Contains('.')))
        {
            return null;
        }
        return $"{uri.GetLeftPart(UriPartial.Path).TrimEnd('/')}/bills/new?receipt={draftId}";
    }

    private async Task<bool> MentionsBotAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var caption = message.Caption;
        if (string.IsNullOrEmpty(caption) || message.CaptionEntities is not { Length: > 0 } entities)
        {
            return false;
        }

        if (entities.Any(e => e.Type == MessageEntityType.TextMention && e.User?.Id == botClient.BotId))
        {
            return true;
        }

        var username = await _botInfo.GetBotUsernameAsync(cancellationToken);
        if (string.IsNullOrEmpty(username))
        {
            return false;
        }
        var mention = "@" + username.TrimStart('@');
        return entities.Any(e =>
            e.Type == MessageEntityType.Mention
            && e.Offset >= 0 && e.Offset + e.Length <= caption.Length
            && string.Equals(caption.Substring(e.Offset, e.Length), mention, StringComparison.OrdinalIgnoreCase));
    }

    private record ImageFile(string Id, long? Size, string? Name);

    private static ImageFile Largest(Message message)
    {
        if (message.Photo is { Length: > 0 } sizes)
        {
            var largest = sizes.OrderByDescending(p => (long)p.Width * p.Height).ThenByDescending(p => p.FileSize ?? 0).First();
            return new(largest.FileId, largest.FileSize, null);
        }
        var document = message.Document!;
        return new(document.FileId, document.FileSize, document.FileName);
    }

    /// <summary>The file into memory only, never to disk; refused when it's over the limit.</summary>
    private static async Task<byte[]?> DownloadAsync(ITelegramBotClient botClient, string fileId, long maxBytes, CancellationToken cancellationToken)
    {
        var file = await botClient.GetFile(fileId, cancellationToken);
        if (file.FileSize > maxBytes)
        {
            return null;
        }
        using var stream = new MemoryStream();
        await botClient.DownloadFile(file.FilePath!, stream, cancellationToken);
        return stream.ToArray();
    }

    private Task ReplyAsync(Message message, string html, List<List<InlineButtonRecord>>? buttons = null) =>
        _publish.Publish(new TelegramMessageRequested(
            message.Chat.Id,
            html,
            InlineKeyboard: buttons,
            ThreadId: message.IsTopicMessage ? message.MessageThreadId : null,
            ReplyToMessageId: message.MessageId));

    /// <summary>Telegram's HTML mode needs only these three escaped.</summary>
    public static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
