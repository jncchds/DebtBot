using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Models.User;
using DebtBot.Receipts;
using DebtBot.Services;
using DebtBot.Telegram;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgUser = Telegram.Bot.Types.User;
using User = DebtBot.DB.Entities.User;

namespace DebtBot.Tests;

/// <summary>
/// Receipts T2: a receipt photo sent to the bot, with fake Telegram updates, the fake LLM client and the real receipt
/// service, limiter and drafts (the same host as the web endpoints).
/// </summary>
public sealed class ReceiptBotTests : IAsyncDisposable
{
    private const long BotId = 4242;
    private const string BotName = "debt_bot";
    private const string WebUrl = "https://debts.example.com";
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1];
    private static readonly byte[] Heic = [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'h', (byte)'e', (byte)'i', (byte)'c'];
    private static readonly byte[] Gif = "GIF89a......"u8.ToArray();

    private ApiTokenHost _h;
    private User _anna = null!;
    private User _boris = null!;
    private User _carl = null!;

    private readonly ITelegramBotClient _bot = Substitute.For<ITelegramBotClient>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();
    private readonly List<TelegramMessageRequested> _sent = [];
    private readonly List<string> _downloads = [];
    private byte[] _file = Jpeg;

    public ReceiptBotTests()
    {
        _h = Start();
        _bot.BotId.Returns(BotId);
        _bot.SendRequest(Arg.Any<IRequest<TGFile>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = (GetFileRequest)call.Arg<IRequest<TGFile>>();
            return new TGFile { FileId = request.FileId, FilePath = $"photos/{request.FileId}.jpg", FileSize = _file.Length };
        });
        _bot.DownloadFile(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _downloads.Add(call.Arg<string>());
            call.Arg<Stream>().Write(_file);
            return Task.CompletedTask;
        });
        _publish.When(p => p.Publish(Arg.Any<TelegramMessageRequested>(), Arg.Any<CancellationToken>()))
            .Do(call => _sent.Add(call.Arg<TelegramMessageRequested>()));
    }

    private ApiTokenHost Start(IDictionary<string, string?>? settings = null)
    {
        var all = new Dictionary<string, string?>
        {
            ["DebtBot:Llm:Provider"] = "Fake",
            ["DebtBot:Llm:UserHourlyLimit"] = "100",
            ["DebtBot:Llm:UserDailyLimit"] = "100",
            ["DebtBot:WebUrl"] = WebUrl,
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            all[key] = value;
        }

        var host = ApiTokenHost.Start(settings: all);
        _anna = host.F.AddUser("Anna", telegramId: 1001, telegramUserName: "@anna", botEnabled: true);
        _boris = host.F.AddUser("Boris", telegramId: 1002, botEnabled: true);
        _carl = host.F.AddUser("Carl", telegramId: 1003, telegramUserName: "@carl"); // never started the bot
        return host;
    }

    private async Task Restart(IDictionary<string, string?> settings)
    {
        await _h.DisposeAsync();
        _h = Start(settings);
    }

    public ValueTask DisposeAsync() => _h.DisposeAsync();

    private FakeLlmClient Fake => _h.Fake!;

    private sealed class FixedBotInfo(IServiceProvider services) : TelegramBotInfoProvider(
        services.GetRequiredService<IServiceScopeFactory>(),
        services.GetRequiredService<IOptions<DebtBotConfiguration>>(),
        NullLogger<TelegramBotInfoProvider>.Instance)
    {
        public override Task<string?> GetBotUsernameAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(BotName);
    }

    private async Task<bool> Handle(Message message)
    {
        using var scope = _h.Services.CreateScope();
        var handler = new ReceiptPhotoHandler(
            scope.ServiceProvider.GetRequiredService<IReceiptService>(),
            _h.Services.GetRequiredService<ReceiptFeature>(),
            _h.F.UserService,
            _h.F.Db,
            _publish,
            new FixedBotInfo(_h.Services),
            _h.Services.GetRequiredService<IOptions<DebtBotConfiguration>>(),
            _h.Time,
            NullLogger<ReceiptPhotoHandler>.Instance);
        return await handler.HandleAsync(_bot, message, default);
    }

    private static Message Photo(long chatId = 1001, long from = 1001, ChatType type = ChatType.Private, string? caption = null,
        MessageEntity[]? entities = null) => new()
    {
        Id = 50,
        Chat = new Chat { Id = chatId, Type = type },
        From = new TgUser { Id = from, FirstName = "Anna" },
        Photo =
        [
            new PhotoSize { FileId = "small", FileUniqueId = "s", Width = 90, Height = 120, FileSize = 1_000 },
            new PhotoSize { FileId = "big", FileUniqueId = "b", Width = 960, Height = 1280, FileSize = 100_000 },
            new PhotoSize { FileId = "medium", FileUniqueId = "m", Width = 320, Height = 427, FileSize = 10_000 },
        ],
        Caption = caption,
        CaptionEntities = entities,
    };

    private static Message GroupPhoto(string caption, params MessageEntity[] entities) =>
        Photo(chatId: -500, type: ChatType.Supergroup, caption: caption, entities: entities);

    private static MessageEntity Mention(string caption, string mention) =>
        new() { Type = MessageEntityType.Mention, Offset = caption.IndexOf(mention, StringComparison.Ordinal), Length = mention.Length };

    private static Message Document(string fileName, string mimeType, long chatId = 1001) => new()
    {
        Id = 51,
        Chat = new Chat { Id = chatId, Type = ChatType.Private },
        From = new TgUser { Id = 1001, FirstName = "Anna" },
        Document = new Document { FileId = "doc", FileUniqueId = "d", FileName = fileName, MimeType = mimeType, FileSize = 2_000 },
    };

    private TelegramMessageRequested Reply() => Assert.Single(_sent);

    private static string? ButtonUrl(TelegramMessageRequested message) =>
        message.InlineKeyboard?.SelectMany(row => row).SingleOrDefault(b => b.text == ReceiptPhotoHandler.WebFormButton)?.url;

    /// <summary>The bill inside the reply's code block, as the user copies it.</summary>
    private static string CopiedBill(TelegramMessageRequested message)
    {
        var start = message.Text.IndexOf("<pre>", StringComparison.Ordinal) + 5;
        var end = message.Text.IndexOf("</pre>", StringComparison.Ordinal);
        return WebUtility.HtmlDecode(message.Text[start..end]);
    }

    private static Guid DraftId(string url) => Guid.Parse(url[(url.IndexOf("receipt=", StringComparison.Ordinal) + 8)..]);

    // --- a private photo ---

    [Fact]
    public async Task A_private_photo_gives_the_bill_text_and_the_web_button()
    {
        Assert.True(await Handle(Photo()));

        var reply = Reply();
        Assert.Equal(1001, reply.ChatId);
        Assert.Equal(50, reply.ReplyToMessageId);
        Assert.Equal(ParseMode.Html, reply.ParseMode);
        Assert.StartsWith("<pre>/AddBill\nCorner Café\n\n39.50\nEUR\n\n39.50 @anna\n\nBorscht ×2\n13.80\n1 @anna", reply.Text);
        Assert.Contains(ReceiptPhotoHandler.PeopleHint, reply.Text);
        Assert.Contains(ReceiptPhotoHandler.PrivacyNote(FakeLlmClient.ModelName), reply.Text);

        var url = ButtonUrl(reply)!;
        Assert.StartsWith($"{WebUrl}/bills/new?receipt=", url);
        Assert.Equal(["photos/big.jpg"], _downloads); // the largest size
        Assert.Single(Fake.Requests);
    }

    [Fact]
    public async Task The_button_opens_the_draft_for_the_same_user_only()
    {
        await Handle(Photo());
        var id = DraftId(ButtonUrl(Reply())!);

        var mine = await _h.Send(HttpMethod.Get, $"/api/v1/Bills/receipt/{id}", _h.Jwt(_anna));
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var draft = JsonDocument.Parse(await mine.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Corner Café", draft.GetProperty("description").GetString());

        var theirs = await _h.Send(HttpMethod.Get, $"/api/v1/Bills/receipt/{id}", _h.Jwt(_boris));
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
    }

    [Fact]
    public async Task Without_a_web_url_there_is_no_button()
    {
        await Restart(new Dictionary<string, string?> { ["DebtBot:WebUrl"] = "" });

        await Handle(Photo());

        var reply = Reply();
        Assert.Null(reply.InlineKeyboard);
        Assert.StartsWith("<pre>/AddBill\n", reply.Text);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("http://localhost:8080", null)]
    [InlineData("http://127.0.0.1:8080", null)]
    [InlineData("http://debtbot:8080", null)]
    [InlineData("ftp://debts.example.com", null)]
    [InlineData("not a url", null)]
    [InlineData("https://debts.example.com", "https://debts.example.com/bills/new?receipt=")]
    [InlineData("https://debts.example.com/", "https://debts.example.com/bills/new?receipt=")]
    [InlineData("https://example.com/debts/", "https://example.com/debts/bills/new?receipt=")]
    [InlineData("http://192.168.1.5:8080", "http://192.168.1.5:8080/bills/new?receipt=")]
    public void The_web_form_url_leaves_out_links_telegram_refuses(string webUrl, string? expectedPrefix)
    {
        var id = Guid.NewGuid();
        var url = ReceiptPhotoHandler.WebFormUrl(webUrl, id);
        Assert.Equal(expectedPrefix is null ? null : expectedPrefix + id, url);
    }

    [Fact]
    public async Task A_supermarket_discount_reaches_the_reply_folded_with_the_warning()
    {
        await Handle(Photo(caption: "supermarket-discount"));

        var reply = Reply();
        var bill = CopiedBill(reply);
        Assert.DoesNotContain("-1.00", bill);
        Assert.Contains("Milk 1L\n1.19\n1 @anna", bill);
        Assert.Contains("⚠️ The discount “Loyalty discount” (1.00 EUR) was spread over all lines.", reply.Text);
    }

    [Fact]
    public async Task The_copied_text_sent_back_as_a_message_parses_as_the_bill()
    {
        await Handle(Photo(caption: "supermarket-discount"));
        var bill = CopiedBill(Reply());

        // What Telegram sends when the user pastes it back: the command and the @mentions as entities.
        var entities = new List<MessageEntity> { new() { Type = MessageEntityType.BotCommand, Offset = 0, Length = "/AddBill".Length } };
        for (var at = bill.IndexOf("@anna", StringComparison.Ordinal); at >= 0; at = bill.IndexOf("@anna", at + 1, StringComparison.Ordinal))
        {
            entities.Add(new MessageEntity { Type = MessageEntityType.Mention, Offset = at, Length = 5 });
        }
        var message = new Message
        {
            Text = bill,
            Entities = entities.ToArray(),
            From = new TgUser { Id = 1001, FirstName = "Anna", Username = "anna" },
            Chat = new Chat { Id = 1001, Type = ChatType.Private },
        };
        var service = new TelegramService(_h.F.UserService);
        var processed = service.ProcessMessage(message, BotId)!;
        Assert.Equal("/AddBill", processed.BotCommand);

        var parsed = service.ParseBill(processed.ProcessedText, processed.UserSearchModels);

        Assert.True(parsed.IsValid, string.Join("; ", parsed.Errors));
        var result = parsed.Result!;
        Assert.Equal("FreshMart 2026-11-21", result.Description);
        Assert.Equal("EUR", result.CurrencyCode);
        Assert.Equal(12.06m, result.TotalWithTips);
        Assert.Equal([1.19m, 2.30m, 5.16m, 3.41m], result.Lines.Select(l => l.Subtotal));
        Assert.All(result.Lines, l => Assert.Equal("@anna", Assert.Single(l.Participants).User.TelegramUserName));

        // And the bill is made from it, with Anna as the payer and the participant.
        var billId = _h.F.BillService.Add(result, new UserSearchModel { TelegramId = 1001 });
        var saved = _h.F.Db.Bills.Single(b => b.Id == billId);
        Assert.Equal(12.06m, saved.TotalWithTips);
        Assert.Equal(_anna.Id, saved.CreatorId);
    }

    [Fact]
    public async Task An_image_sent_as_a_document_is_read()
    {
        Assert.True(await Handle(Document("tips-outside.png", "image/png")));

        Assert.StartsWith("<pre>/AddBill\n", Reply().Text);
        Assert.Equal(["photos/doc.jpg"], _downloads);
    }

    [Fact]
    public async Task A_document_that_isnt_an_image_is_not_ours()
    {
        Assert.False(await Handle(Document("notes.pdf", "application/pdf")));
        Assert.Empty(_sent);
        Assert.Empty(Fake.Requests);
    }

    // --- groups ---

    [Fact]
    public async Task A_group_photo_without_a_mention_is_ignored_and_never_reaches_the_model()
    {
        Assert.False(await Handle(GroupPhoto("lunch!")));
        Assert.False(await Handle(Photo(chatId: -500, type: ChatType.Group)));
        var other = "for @boris";
        Assert.False(await Handle(GroupPhoto(other, Mention(other, "@boris"))));

        Assert.Empty(_sent);
        Assert.Empty(_downloads);
        Assert.Empty(Fake.Requests);
    }

    [Fact]
    public async Task A_group_photo_that_mentions_the_bot_is_read()
    {
        var caption = "@Debt_Bot lunch";
        Assert.True(await Handle(GroupPhoto(caption, Mention(caption, "@Debt_Bot"))));

        var reply = Reply();
        Assert.Equal(-500, reply.ChatId);
        Assert.StartsWith("<pre>/AddBill\n", reply.Text);
        Assert.Single(Fake.Requests);
    }

    [Fact]
    public async Task A_text_mention_of_the_bot_counts_too()
    {
        var caption = "Debt bot, please";
        var entity = new MessageEntity { Type = MessageEntityType.TextMention, Offset = 0, Length = 8, User = new TgUser { Id = BotId, IsBot = true, FirstName = "Debt bot" } };

        Assert.True(await Handle(GroupPhoto(caption, entity)));
        Assert.Single(Fake.Requests);
    }

    // --- the privacy note ---

    [Fact]
    public async Task The_privacy_note_comes_once_per_chat_then_the_footer()
    {
        var note = ReceiptPhotoHandler.PrivacyNote(FakeLlmClient.ModelName);
        var footer = ReceiptPhotoHandler.PrivacyFooter(FakeLlmClient.ModelName);

        await Handle(Photo());
        await Handle(Photo());
        var caption = "@debt_bot";
        await Handle(GroupPhoto(caption, Mention(caption, "@debt_bot")));

        Assert.Equal(3, _sent.Count);
        Assert.Contains(note, _sent[0].Text);
        Assert.DoesNotContain(footer, _sent[0].Text);
        Assert.Contains(footer, _sent[1].Text);
        Assert.DoesNotContain(note, _sent[1].Text);
        Assert.Contains(note, _sent[2].Text); // another chat gets it once too

        // Kept in the database, not in memory.
        _h.F.Clear();
        Assert.Equal([-500L, 1001L], _h.F.Db.TelegramChatStates.Where(s => s.ReceiptNoteShownAt != null).Select(s => s.ChatId).OrderBy(c => c).ToList());
    }

    [Fact]
    public async Task The_note_isnt_used_up_by_a_photo_that_never_reached_the_model()
    {
        _file = Gif;
        await Handle(Photo());
        _file = Jpeg;
        await Handle(Photo());

        Assert.DoesNotContain("AI service", _sent[0].Text);
        Assert.Contains(ReceiptPhotoHandler.PrivacyNote(FakeLlmClient.ModelName), _sent[1].Text);
    }

    // --- who may ---

    [Fact]
    public async Task A_sender_who_never_started_the_bot_is_asked_to_start_it()
    {
        Assert.True(await Handle(Photo(from: 1003)));
        Assert.Equal(ReceiptPhotoHandler.StartFirst, Reply().Text);
        Assert.Empty(Fake.Requests);
        Assert.Empty(_downloads);
    }

    [Fact]
    public async Task An_unknown_sender_is_asked_to_start_the_bot()
    {
        Assert.True(await Handle(Photo(from: 9999, chatId: 9999)));
        Assert.Equal(ReceiptPhotoHandler.StartFirst, Reply().Text);
        Assert.Empty(Fake.Requests);
    }

    [Fact]
    public async Task Without_a_key_a_private_chat_gets_one_line_and_a_group_nothing()
    {
        await Restart(new Dictionary<string, string?> { ["DebtBot:Llm:Provider"] = "OpenRouter", ["DebtBot:Llm:ApiKey"] = "" });

        Assert.True(await Handle(Photo()));
        Assert.Equal(ReceiptErrors.Message(ReceiptError.Unavailable), Reply().Text);

        _sent.Clear();
        var caption = "@debt_bot";
        Assert.True(await Handle(GroupPhoto(caption, Mention(caption, "@debt_bot"))));
        Assert.Empty(_sent);
        Assert.Empty(_downloads);
    }

    [Fact]
    public async Task An_edited_caption_isnt_read_again()
    {
        var message = Photo();
        message.EditDate = DateTime.UtcNow;

        Assert.True(await Handle(message));
        Assert.Empty(_sent);
        Assert.Empty(Fake.Requests);
    }

    // --- errors and limits ---

    [Theory]
    [InlineData("not-a-receipt", "This doesn't look like a receipt.")]
    [InlineData("broken-json", "The receipt couldn't be read. Try a sharper photo or fill the bill in by hand.")]
    [InlineData("busy", "The free AI service is busy or out of quota. Try later or fill the bill in by hand.")]
    [InlineData("timeout", "The AI service took too long. Try again or fill the bill in by hand.")]
    [InlineData("upstream", "The AI service failed. Try again or fill the bill in by hand.")]
    public async Task Model_errors_get_the_webs_messages(string hint, string message)
    {
        await Handle(Photo(caption: hint));

        var reply = Reply();
        Assert.StartsWith(message, reply.Text);
        Assert.Null(reply.InlineKeyboard);
        Assert.Contains("AI service (OpenRouter", reply.Text); // it did reach the model
    }

    [Fact]
    public void Every_error_code_has_its_message()
    {
        foreach (var error in Enum.GetValues<ReceiptError>())
        {
            Assert.Equal(ReceiptErrors.Message(error), ReceiptPhotoHandler.ErrorText(error, null));
        }
    }

    [Fact]
    public async Task A_photo_over_the_limit_is_too_large_and_not_downloaded()
    {
        var message = Photo();
        message.Photo![1].FileSize = 5 * 1024 * 1024 + 1;

        await Handle(message);

        Assert.Equal(ReceiptErrors.Message(ReceiptError.TooLarge), Reply().Text);
        Assert.Empty(_downloads);
        Assert.Empty(Fake.Requests);
    }

    [Fact]
    public async Task A_file_over_the_limit_by_getFile_is_too_large()
    {
        _file = new byte[5 * 1024 * 1024 + 1];
        var message = Photo();
        message.Photo![1].FileSize = null; // Telegram doesn't always say

        await Handle(message);

        Assert.Equal(ReceiptErrors.Message(ReceiptError.TooLarge), Reply().Text);
        Assert.Empty(_downloads);
    }

    [Fact]
    public async Task Heic_and_other_types_get_their_messages()
    {
        _file = Heic;
        await Handle(Document("IMG_1.heic", "image/heic"));
        _file = Gif;
        await Handle(Document("a.gif", "image/gif"));

        Assert.Equal(ReceiptPhotoHandler.Escape(ReceiptErrors.Message(ReceiptError.Heic)), _sent[0].Text);
        Assert.Equal(ReceiptErrors.Message(ReceiptError.UnsupportedType), _sent[1].Text);
        Assert.Empty(Fake.Requests);
    }

    [Fact]
    public async Task The_limit_is_shared_with_the_web()
    {
        await Restart(new Dictionary<string, string?> { ["DebtBot:Llm:UserHourlyLimit"] = "3" });

        // One on the web…
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Jpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(content, "image", "receipt.jpg");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/Bills/receipt") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _h.Jwt(_anna));
        Assert.Equal(HttpStatusCode.OK, (await _h.Client.SendAsync(request)).StatusCode);

        // …and two in the bot use up the hour.
        await Handle(Photo());
        await Handle(Photo());
        await Handle(Photo());

        Assert.Equal(3, _sent.Count);
        Assert.StartsWith("<pre>", _sent[1].Text);
        Assert.Equal(
            "You've read too many receipts for now. Try later or fill the bill in by hand. You can read the next one in 1 hour.",
            _sent[2].Text);
        Assert.Equal(3, Fake.Requests.Count); // the refused one never reached the model

        // Boris has his own count.
        _sent.Clear();
        await Handle(Photo(from: 1002, chatId: 1002));
        Assert.StartsWith("<pre>", Reply().Text);
    }

    [Theory]
    [InlineData(30, "1 minute")]
    [InlineData(60, "1 minute")]
    [InlineData(61, "2 minutes")]
    [InlineData(42 * 60, "42 minutes")]
    [InlineData(3600, "1 hour")]
    [InlineData(3601, "2 hours")]
    [InlineData(5 * 3600, "5 hours")]
    public void Retry_times_read_as_minutes_or_hours(int seconds, string expected) =>
        Assert.Equal(expected, ReceiptPhotoHandler.Duration(TimeSpan.FromSeconds(seconds)));

    // --- the reply's parts ---

    [Fact]
    public void Warnings_take_at_most_two_lines()
    {
        var warnings = Enumerable.Range(1, 4).Select(i => new Models.Receipt.ReceiptWarning("x") { Message = $"W{i}." }).ToList();

        Assert.Equal("", ReceiptPhotoHandler.WarningLines([]));
        Assert.Equal("⚠️ W1.", ReceiptPhotoHandler.WarningLines(warnings.Take(1).ToList()));
        Assert.Equal("⚠️ W1.\n⚠️ W2. (+2 more)", ReceiptPhotoHandler.WarningLines(warnings));
    }

    [Fact]
    public void The_reply_escapes_html_and_stays_within_telegrams_limit()
    {
        var draft = new Models.Receipt.ReceiptDraft { Text = "Fish & <Chips>\n\n10.00\nGBP" };
        Assert.StartsWith("<pre>/AddBill\nFish &amp; &lt;Chips&gt;\n", ReceiptPhotoHandler.BuildReply(draft, "<i>note</i>"));

        var huge = new Models.Receipt.ReceiptDraft { Text = string.Concat(Enumerable.Repeat("Line\n1.00\n1 @anna\n\n", 400)) };
        var reply = ReceiptPhotoHandler.BuildReply(huge, "<i>note</i>");
        Assert.True(reply.Length <= ReceiptPhotoHandler.MaxMessageLength);
        Assert.DoesNotContain("<pre>", reply);
        Assert.Contains("too long for one Telegram message", reply);
    }

    // --- through the update handler ---

    [Fact]
    public async Task The_update_handler_sends_photos_to_the_receipt_handler()
    {
        using var scope = _h.Services.CreateScope();
        var photos = new ReceiptPhotoHandler(
            scope.ServiceProvider.GetRequiredService<IReceiptService>(),
            _h.Services.GetRequiredService<ReceiptFeature>(),
            _h.F.UserService, _h.F.Db, _publish, new FixedBotInfo(_h.Services),
            _h.Services.GetRequiredService<IOptions<DebtBotConfiguration>>(), _h.Time,
            NullLogger<ReceiptPhotoHandler>.Instance);
        var telegramService = Substitute.For<ITelegramService>();
        var handler = new UpdateHandler(_bot, NullLogger<UpdateHandler>.Instance, [], [], telegramService, _publish, photos);

        await handler.HandleUpdateAsync(_bot, new Update { Message = Photo() }, default);

        telegramService.Received(1).Actualize(Arg.Is<TgUser>(u => u.Id == 1001));
        telegramService.DidNotReceiveWithAnyArgs().ProcessMessage(default!, default);
        Assert.StartsWith("<pre>/AddBill\n", Reply().Text);
    }
}
