using DebtBot.Interfaces;
using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using DebtBot.Services;
using DebtBot.Telegram;
using DebtBot.Telegram.Commands.Message;
using MassTransit;
using NSubstitute;
using Telegram.Bot;

namespace DebtBot.Tests;

public class BearerCommandTests
{
    private const long SenderTelegramId = 1001;

    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();
    private readonly BearerCommand _command;

    private readonly UserModel _mentioned = new() { Id = Guid.NewGuid(), DisplayName = "Boris", TelegramUserName = "@boris" };

    public BearerCommandTests()
    {
        _command = new BearerCommand(_userService, _identityService, Substitute.For<ITelegramService>(), _publish);
        _identityService.GenerateJwtToken(Arg.Any<UserModel>()).Returns(call => $"token-for-{call.Arg<UserModel>().Id}");
        _userService.FindUser(Arg.Is<UserSearchModel>(m => m.TelegramUserName == "@boris"), Arg.Any<Guid?>()).Returns(_mentioned);
    }

    private UserModel GivenSender(UserRole role)
    {
        var sender = new UserModel { Id = Guid.NewGuid(), DisplayName = "Anna", TelegramId = SenderTelegramId, Role = role };
        _userService.FindUser(Arg.Is<UserSearchModel>(m => m.TelegramId == SenderTelegramId), Arg.Any<Guid?>()).Returns(sender);
        return sender;
    }

    private static ProcessedMessage Message(bool withMention) => new()
    {
        ChatId = 1,
        FromId = SenderTelegramId,
        ProcessedText = withMention ? "/Bearer @boris" : "/Bearer",
        UserSearchModels = withMention ? [new UserSearchModel { TelegramUserName = "@boris" }] : [],
    };

    private string? PublishedText()
    {
        var call = _publish.ReceivedCalls().Single();
        return ((TelegramMessageRequested)call.GetArguments()[0]!).Text;
    }

    [Fact]
    public async Task NonAdminWithMention_GetsOwnToken()
    {
        var sender = GivenSender(UserRole.Regular);

        await _command.ExecuteAsync(Message(withMention: true), Substitute.For<ITelegramBotClient>(), default);

        _identityService.Received(1).GenerateJwtToken(Arg.Is<UserModel>(u => u.Id == sender.Id));
        _identityService.DidNotReceive().GenerateJwtToken(Arg.Is<UserModel>(u => u.Id == _mentioned.Id));
        Assert.Contains($"token-for-{sender.Id}", PublishedText());
    }

    [Fact]
    public async Task NonAdminWithoutMention_GetsOwnToken()
    {
        var sender = GivenSender(UserRole.Regular);

        await _command.ExecuteAsync(Message(withMention: false), Substitute.For<ITelegramBotClient>(), default);

        Assert.Contains($"token-for-{sender.Id}", PublishedText());
    }

    [Fact]
    public async Task AdminWithMention_GetsMentionedUsersToken()
    {
        GivenSender(UserRole.Admin);

        await _command.ExecuteAsync(Message(withMention: true), Substitute.For<ITelegramBotClient>(), default);

        Assert.Contains($"token-for-{_mentioned.Id}", PublishedText());
    }

    [Fact]
    public async Task AdminWithoutMention_GetsOwnToken()
    {
        var sender = GivenSender(UserRole.Admin);

        await _command.ExecuteAsync(Message(withMention: false), Substitute.For<ITelegramBotClient>(), default);

        Assert.Contains($"token-for-{sender.Id}", PublishedText());
    }

    [Fact]
    public async Task UnknownSender_GetsNoToken()
    {
        await _command.ExecuteAsync(Message(withMention: true), Substitute.For<ITelegramBotClient>(), default);

        _identityService.DidNotReceiveWithAnyArgs().GenerateJwtToken(default!);
        Assert.Equal("User not found", PublishedText());
    }
}
