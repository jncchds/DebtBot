using System.Text;
using System.Text.Json;
using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Models.Enums;
using DebtBot.Services;
using DebtBot.Telegram;
using DebtBot.Telegram.Commands.CallbackQueries;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Tests;

/// <summary>
/// The bot code paths that Telegram.Bot 22 changed: sending and editing messages, answering callbacks,
/// the exchange Finalize button and parsing incoming updates.
/// </summary>
public class TelegramBotApiTests
{
    private const long BotId = 4242;

    private readonly ITelegramBotClient _bot = Substitute.For<ITelegramBotClient>();
    private readonly List<object> _requests = new();

    public TelegramBotApiTests()
    {
        _bot.BotId.Returns(BotId);
        _bot.SendRequest(Arg.Any<IRequest<Message>>(), Arg.Any<CancellationToken>())
            .Returns(call => { _requests.Add(call.Arg<IRequest<Message>>()); return new Message(); });
        _bot.SendRequest(Arg.Any<IRequest<bool>>(), Arg.Any<CancellationToken>())
            .Returns(call => { _requests.Add(call.Arg<IRequest<bool>>()); return true; });
    }

    [Fact]
    public async Task NewMessage_WithInlineKeyboard_SendsInlineMarkupAndReplyParameters()
    {
        var message = new TelegramMessageRequested(
            ChatId: 77,
            Text: "<b>Exchange</b>",
            InlineKeyboard: [[new InlineButtonRecord("Finalize", "/FinalizeExchange abc")]],
            ReplyKeyboard: [["/Debts"]],
            ThreadId: 5,
            ReplyToMessageId: 12);

        await _bot.SendOrUpdateTelegramMessage(message, default);

        var request = Assert.IsType<SendMessageRequest>(Assert.Single(_requests));
        Assert.Equal(77, request.ChatId?.Identifier);
        Assert.Equal("<b>Exchange</b>", request.Text);
        Assert.Equal(ParseMode.Html, request.ParseMode);
        Assert.Equal(5, request.MessageThreadId);
        Assert.Equal(12, request.ReplyParameters?.MessageId);
        // The inline keyboard wins over the reply keyboard, as before.
        var markup = Assert.IsType<InlineKeyboardMarkup>(request.ReplyMarkup);
        var button = Assert.Single(markup.InlineKeyboard.SelectMany(row => row));
        Assert.Equal("Finalize", button.Text);
        Assert.Equal("/FinalizeExchange abc", button.CallbackData);
    }

    [Fact]
    public async Task NewMessage_WithReplyKeyboardOnly_SendsResizedReplyKeyboard()
    {
        var message = new TelegramMessageRequested(1, "Hi", ReplyKeyboard: TelegramBotExtensions.DefaultReplyKeyboard);

        await _bot.SendOrUpdateTelegramMessage(message, default);

        var request = Assert.IsType<SendMessageRequest>(Assert.Single(_requests));
        Assert.Null(request.ReplyParameters);
        Assert.Null(request.MessageThreadId);
        var markup = Assert.IsType<ReplyKeyboardMarkup>(request.ReplyMarkup);
        Assert.True(markup.ResizeKeyboard);
        Assert.Equal(["/Debts", "/Bills", "/Spendings"], markup.Keyboard.Single().Select(b => b.Text));
    }

    [Fact]
    public async Task LinkButton_IsSentAsUrl()
    {
        var message = new TelegramMessageRequested(1, "<pre>bill</pre>",
            InlineKeyboard: [[new InlineButtonRecord("Open in the web form", "", "https://debts.example.com/bills/new?receipt=1")]]);

        await _bot.SendOrUpdateTelegramMessage(message, default);

        var request = Assert.IsType<SendMessageRequest>(Assert.Single(_requests));
        var button = Assert.IsType<InlineKeyboardMarkup>(request.ReplyMarkup).InlineKeyboard.Single().Single();
        Assert.Equal("https://debts.example.com/bills/new?receipt=1", button.Url);
        Assert.Null(button.CallbackData);
    }

    [Fact]
    public async Task RefusedLinkButton_IsLeftOutAndTheMessageStillSent()
    {
        var attempts = new List<SendMessageRequest>();
        _bot.SendRequest(Arg.Any<IRequest<Message>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = (SendMessageRequest)call.Arg<IRequest<Message>>();
            attempts.Add(request);
            if (request.ReplyMarkup is InlineKeyboardMarkup markup && markup.InlineKeyboard.SelectMany(r => r).Any(b => b.Url is not null))
            {
                throw new global::Telegram.Bot.Exceptions.ApiRequestException("Bad Request: BUTTON_URL_INVALID", 400);
            }
            return new Message();
        });
        var message = new TelegramMessageRequested(1, "<pre>bill</pre>",
            InlineKeyboard: [[new InlineButtonRecord("Open in the web form", "", "http://debtbot/bills/new?receipt=1")]]);

        await _bot.SendOrUpdateTelegramMessage(message, default);

        Assert.Equal(2, attempts.Count);
        Assert.Equal("<pre>bill</pre>", attempts[1].Text);
        Assert.Null(attempts[1].ReplyMarkup);
    }

    [Fact]
    public async Task MessageWithId_IsEdited()
    {
        var message = new TelegramMessageRequested(9, "page 2", InlineKeyboard: [[new InlineButtonRecord(">", "/Debts 2")]],
            MessageId: 31, ParseMode: ParseMode.Markdown);

        await _bot.SendOrUpdateTelegramMessage(message, default);

        var request = Assert.IsType<EditMessageTextRequest>(Assert.Single(_requests));
        Assert.Equal(9, request.ChatId.Identifier);
        Assert.Equal(31, request.MessageId);
        Assert.Equal("page 2", request.Text);
        Assert.Equal(ParseMode.Markdown, request.ParseMode);
        Assert.Equal("/Debts 2", request.ReplyMarkup!.InlineKeyboard.Single().Single().CallbackData);
    }

    [Fact]
    public void FinalizeExchangeCallbackData_FitsTelegramLimit()
    {
        var data = FinalizeExchangeCommand.FormatCallbackData(Guid.NewGuid(), Guid.NewGuid());

        // Telegram allows at most 64 bytes of callback data.
        Assert.True(Encoding.UTF8.GetByteCount(data) <= 64, data);
        Assert.StartsWith("/FinalizeExchange ", data);
    }

    [Fact]
    public async Task FinalizeExchangeButton_ThroughUpdateHandler_FinalizesBothBillsAndAnswers()
    {
        var billService = Substitute.For<IBillService>();
        billService.FinalizeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        billService.CanTelegramUserChangeAsync(Arg.Any<Guid>(), 1001, Arg.Any<CancellationToken>()).Returns(true);
        var publish = Substitute.For<IPublishEndpoint>();
        var forward = Guid.NewGuid();
        var backward = Guid.NewGuid();
        var handler = Handler(callbackQueries: [new FinalizeExchangeCommand(billService, publish)]);

        var update = new Update
        {
            CallbackQuery = new CallbackQuery
            {
                Id = "cb1",
                From = new User { Id = 1001, FirstName = "Anna" },
                Data = FinalizeExchangeCommand.FormatCallbackData(forward, backward),
                Message = new Message { Chat = new Chat { Id = 555, Type = ChatType.Private } },
            }
        };

        await handler.HandleUpdateAsync(_bot, update, default);

        Received.InOrder(() =>
        {
            billService.FinalizeAsync(forward, Arg.Any<CancellationToken>());
            billService.FinalizeAsync(backward, Arg.Any<CancellationToken>());
        });
        await publish.DidNotReceiveWithAnyArgs().Publish<TelegramMessageRequested>(default!, default);
        var answer = Assert.IsType<AnswerCallbackQueryRequest>(Assert.Single(_requests));
        Assert.Equal("cb1", answer.CallbackQueryId);
        Assert.Null(answer.Text);
    }

    [Fact]
    public async Task FinalizeExchangeButton_FailedBill_ReportsToTheChat()
    {
        var billService = Substitute.For<IBillService>();
        var publish = Substitute.For<IPublishEndpoint>();
        var forward = Guid.NewGuid();
        billService.FinalizeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<Guid>() != forward);
        billService.CanTelegramUserChangeAsync(Arg.Any<Guid>(), 1, Arg.Any<CancellationToken>()).Returns(true);
        var command = new FinalizeExchangeCommand(billService, publish);

        var result = await command.ExecuteAsync(new CallbackQuery
        {
            Id = "cb2",
            From = new User { Id = 1 },
            Data = FinalizeExchangeCommand.FormatCallbackData(forward, Guid.NewGuid()),
            Message = new Message { Chat = new Chat { Id = 555 } },
        }, default);

        Assert.Null(result);
        await publish.Received(1).Publish(
            Arg.Is<TelegramMessageRequested>(m => m.ChatId == 555 && m.Text == "There was an error finalizing the bill"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownCallback_IsAnsweredWithCommandNotFound()
    {
        var handler = Handler(callbackQueries: []);

        await handler.HandleUpdateAsync(_bot, new Update
        {
            CallbackQuery = new CallbackQuery { Id = "cb3", From = new User { Id = 1 }, Data = "/Nope 1" }
        }, default);

        var answer = Assert.IsType<AnswerCallbackQueryRequest>(Assert.Single(_requests));
        Assert.Equal("command not found", answer.Text);
    }

    [Fact]
    public async Task Message_IsRoutedToTheCommandCaseInsensitively()
    {
        var command = Substitute.For<ITelegramCommand>();
        command.CommandName.Returns("/ExchangeDebt");
        var telegramService = Substitute.For<ITelegramService>();
        var processed = new ProcessedMessage { BotCommand = "/exchangedebt", ProcessedText = "", UserSearchModels = [] };
        telegramService.ProcessMessage(Arg.Any<Message>(), BotId).Returns(processed);
        var handler = Handler(commands: [command], telegramService: telegramService);

        await handler.HandleUpdateAsync(_bot, new Update
        {
            Message = new Message
            {
                Text = "/exchangedebt",
                From = new User { Id = 1001, FirstName = "Anna" },
                Chat = new Chat { Id = 1001, Type = ChatType.Private },
            }
        }, default);

        telegramService.Received(1).Actualize(Arg.Is<User>(u => u.Id == 1001));
        await command.Received(1).ExecuteAsync(processed, _bot, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void IncomingUpdateJson_ParsesCommandMentionsAndReplyHeader()
    {
        var billId = Guid.NewGuid();
        // A raw Bot API update, as Telegram sends it.
        var json = $$"""
        {
          "update_id": 1,
          "message": {
            "message_id": 10,
            "date": 1763900000,
            "from": { "id": 1001, "is_bot": false, "first_name": "Anna", "username": "anna" },
            "chat": { "id": 1001, "type": "private", "first_name": "Anna" },
            "text": "/AddLines@debt_bot 10 Pizza @boris Carl",
            "entities": [
              { "type": "bot_command", "offset": 0, "length": 18 },
              { "type": "mention", "offset": 28, "length": 6 },
              { "type": "text_mention", "offset": 35, "length": 4, "user": { "id": 3003, "is_bot": false, "first_name": "Carl", "username": "carl" } }
            ],
            "reply_to_message": {
              "message_id": 9,
              "date": 1763899990,
              "from": { "id": {{BotId}}, "is_bot": true, "first_name": "Debt bot" },
              "chat": { "id": 1001, "type": "private" },
              "text": "Bill {{billId}}\nPizza night"
            }
          }
        }
        """;

        var update = JsonSerializer.Deserialize<Update>(json, JsonBotAPI.Options)!;
        var service = new TelegramService(Substitute.For<IUserService>());

        var processed = service.ProcessMessage(update.Message!, BotId)!;

        Assert.Equal("/AddLines@debt_bot", processed.BotCommand);
        Assert.Equal("10 Pizza {0} {1}", processed.ProcessedText);
        Assert.Equal(1001, processed.FromId);
        Assert.Equal(1001, processed.ChatId);
        Assert.Collection(processed.UserSearchModels,
            m => { Assert.Null(m.TelegramId); Assert.Equal("@boris", m.TelegramUserName); },
            m => { Assert.Equal(3003, m.TelegramId); Assert.Equal("carl", m.TelegramUserName); Assert.Equal("Carl", m.DisplayName); });
        Assert.Equal(ObjectType.Bill, processed.ObjectType);
        Assert.Equal(billId, processed.ObjectId);
    }

    private UpdateHandler Handler(
        IEnumerable<ITelegramCommand>? commands = null,
        IEnumerable<ITelegramCallbackQuery>? callbackQueries = null,
        ITelegramService? telegramService = null) =>
        new(_bot,
            NullLogger<UpdateHandler>.Instance,
            commands ?? [],
            callbackQueries ?? [],
            telegramService ?? Substitute.For<ITelegramService>(),
            Substitute.For<IPublishEndpoint>());
}
