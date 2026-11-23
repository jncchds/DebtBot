using System.Security.Claims;
using DebtBot.Controllers;
using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using DebtBot.Telegram;
using DebtBot.Telegram.Commands;
using DebtBot.Telegram.Commands.CallbackQueries;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Types;
using User = DebtBot.DB.Entities.User;

namespace DebtBot.Tests;

/// <summary>Fixes for the bugs T2 found that were already there: user delete, admin user edit, username lookup.</summary>
public sealed class UserFixesTests : IDisposable
{
    private readonly SqliteFixture _f = new();

    public void Dispose() => _f.Dispose();

    private Bill AddBill(User creator)
    {
        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            CurrencyCode = "EUR",
            PaymentCurrencyCode = "EUR",
            Description = "Dinner",
            Date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TotalWithTips = 30m,
            Status = ProcessingState.Processed,
        };
        _f.Db.Bills.Add(bill);
        _f.Db.SaveChanges();
        return bill;
    }

    [Fact]
    public void DeleteUser_Placeholder_IsDeletedWithSubscriptionsAndTokens()
    {
        var anna = _f.AddUser("Anna");
        var placeholder = _f.AddUser("@ghost", telegramUserName: "@ghost");
        _f.Db.NotificationSubscriptions.Add(new NotificationSubscription { UserId = placeholder.Id, SubscriberId = anna.Id });
        _f.Db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = placeholder.Id, TokenHash = "h", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1),
        });
        _f.Db.SaveChanges();
        _f.Clear();

        Assert.Equal(DeleteUserResult.Deleted, _f.UserService.DeleteUser(placeholder.Id));

        _f.Clear();
        Assert.Null(_f.Db.Users.Find(placeholder.Id));
        Assert.Empty(_f.Db.NotificationSubscriptions);
        Assert.NotNull(_f.Db.Users.Find(anna.Id));
    }

    [Fact]
    public void DeleteUser_Unknown_IsNotFound()
    {
        Assert.Equal(DeleteUserResult.NotFound, _f.UserService.DeleteUser(Guid.NewGuid()));
    }

    [Fact]
    public void DeleteUser_BillCreator_IsInUse()
    {
        var anna = _f.AddUser("Anna");
        AddBill(anna);
        _f.Clear();

        Assert.Equal(DeleteUserResult.InUse, _f.UserService.DeleteUser(anna.Id));
        _f.Clear();
        Assert.NotNull(_f.Db.Users.Find(anna.Id));
        Assert.Single(_f.Db.Bills);
    }

    [Fact]
    public void DeleteUser_WithLedgerRecordsOnly_IsInUse()
    {
        var anna = _f.AddUser("Anna");
        var boris = _f.AddUser("Boris");
        var bill = AddBill(anna);
        _f.Db.LedgerRecords.Add(new LedgerRecord
        {
            BillId = bill.Id, CreditorUserId = anna.Id, DebtorUserId = boris.Id, Amount = 10m, CurrencyCode = "EUR",
        });
        _f.Db.SaveChanges();
        _f.Clear();

        Assert.Equal(DeleteUserResult.InUse, _f.UserService.DeleteUser(boris.Id));
    }

    [Fact]
    public void DeleteUser_InSomeonesContacts_IsInUse()
    {
        var anna = _f.AddUser("Anna");
        var boris = _f.AddUser("Boris");
        _f.UserContactService.EnsureContact(anna.Id, boris.Id, "Bob");
        _f.Clear();

        Assert.Equal(DeleteUserResult.InUse, _f.UserService.DeleteUser(boris.Id));
        Assert.Equal(DeleteUserResult.InUse, _f.UserService.DeleteUser(anna.Id));
    }

    [Fact]
    public void FindUser_UnderscoreInUsername_IsNotAWildcard()
    {
        var real = _f.AddUser("Anna", telegramUserName: "@anna_k");
        _f.Clear();

        Assert.Null(_f.UserService.FindUser(new UserSearchModel { TelegramUserName = "@annaxk" }));
        Assert.Null(_f.UserService.FindUser(new UserSearchModel { TelegramUserName = "@anna%" }));
        Assert.Equal(real.Id, _f.UserService.FindUser(new UserSearchModel { TelegramUserName = "@ANNA_K" })?.Id);
        Assert.Equal(real.Id, _f.UserService.FindUser(new UserSearchModel { QueryString = "@Anna_K" })?.Id);
        Assert.Null(_f.UserService.FindUser(new UserSearchModel { QueryString = "@ann__k" }));
    }

    [Fact]
    public void FindUser_PlaceholderForSimilarUsername_IsNewUser()
    {
        var real = _f.AddUser("Anna", telegramUserName: "@anna_k");
        _f.Clear();

        // Before the fix @annaxk matched @anna_k, so a bill for @annaxk landed on Anna.
        var found = _f.UserService.FindOrAddUser(new UserSearchModel { TelegramUserName = "@annaxk", DisplayName = "@annaxk" });

        Assert.NotEqual(real.Id, found.Id);
        Assert.Equal("@annaxk", found.TelegramUserName);
    }

    [Fact]
    public void FindUser_ContactDisplayName_MatchesExactlyIgnoringCase()
    {
        var anna = _f.AddUser("Anna");
        var boris = _f.AddUser("Boris");
        _f.UserContactService.EnsureContact(anna.Id, boris.Id, "Bob_1");
        _f.Clear();

        Assert.Equal(boris.Id, _f.UserService.FindUser(new UserSearchModel { DisplayName = "bob_1" }, anna.Id)?.Id);
        Assert.Null(_f.UserService.FindUser(new UserSearchModel { DisplayName = "Bobx1" }, anna.Id));
    }

    [Fact]
    public void ConfirmAndDecline_WithoutRequest_ReturnFalse()
    {
        var anna = _f.AddUser("Anna");
        var boris = _f.AddUser("Boris");

        Assert.False(_f.UserContactService.ConfirmSubscription(anna.Id, boris.Id));
        Assert.False(_f.UserContactService.DeclineSubscription(anna.Id, boris.Id));

        _f.UserContactService.RequestSubscription(boris.Id, anna.Id);
        Assert.True(_f.UserContactService.ConfirmSubscription(anna.Id, boris.Id));
        Assert.True(_f.UserContactService.DeclineSubscription(anna.Id, boris.Id));
    }

    [Fact]
    public async Task TelegramUserCanChange_CreatorOrAdminOnly()
    {
        var anna = _f.AddUser("Anna", telegramId: 1001);
        _f.AddUser("Boris", telegramId: 1002);
        _f.AddUser("Admin", telegramId: 1003, role: UserRole.Admin);
        var bill = AddBill(anna);
        _f.Clear();

        Assert.True(await _f.BillService.CanTelegramUserChangeAsync(bill.Id, 1001, default));
        Assert.False(await _f.BillService.CanTelegramUserChangeAsync(bill.Id, 1002, default));
        Assert.True(await _f.BillService.CanTelegramUserChangeAsync(bill.Id, 1003, default));
        Assert.False(await _f.BillService.CanTelegramUserChangeAsync(bill.Id, 9999, default));
        // An unknown bill is left to the command, which reports it as before.
        Assert.True(await _f.BillService.CanTelegramUserChangeAsync(Guid.NewGuid(), 1002, default));
    }
}

/// <summary>The admin user endpoints and the subscription endpoints after the fixes.</summary>
public class UserEndpointFixesTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly IUserContactService _contacts = Substitute.For<IUserContactService>();

    private T WithUser<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _me.ToString())], "test")),
            },
        };
        return controller;
    }

    private UsersController Users() => WithUser(new UsersController(_users, Substitute.For<IAuthService>()));
    private UserContactsController Contacts() => WithUser(new UserContactsController(_contacts, _users));

    private static int? Status(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode;

    private static string? Message(IActionResult result) =>
        (result as ObjectResult)?.Value?.GetType().GetProperty("message")?.GetValue(((ObjectResult)result).Value) as string;

    [Fact]
    public void PutUser_DifferentTelegramId_Is400AndChangesNothing()
    {
        var id = Guid.NewGuid();
        _users.GetUserById(id).Returns(new UserModel { Id = id, DisplayName = "Anna", TelegramId = 1001 });

        var result = Users().Put(id, new UserCreationModel { DisplayName = "Anna K", TelegramId = 2002 });

        Assert.Equal(400, Status(result));
        Assert.Equal("The Telegram id can't be changed.", Message(result));
        _users.DidNotReceiveWithAnyArgs().UpdateUser(default, default!);
    }

    [Fact]
    public void PutUser_SameOrNoTelegramId_Updates()
    {
        var id = Guid.NewGuid();
        _users.GetUserById(id).Returns(new UserModel { Id = id, DisplayName = "Anna", TelegramId = 1001 });

        Assert.Equal(200, Status(Users().Put(id, new UserCreationModel { DisplayName = "Anna K", TelegramId = 1001 })));
        Assert.Equal(200, Status(Users().Put(id, new UserCreationModel { DisplayName = "Anna K" })));

        _users.Received(2).UpdateUser(id, Arg.Any<UserCreationModel>());
    }

    [Fact]
    public void PutUser_SettingTelegramIdOnPlaceholder_Is400()
    {
        var id = Guid.NewGuid();
        _users.GetUserById(id).Returns(new UserModel { Id = id, DisplayName = "@ghost", TelegramUserName = "@ghost" });

        Assert.Equal(400, Status(Users().Put(id, new UserCreationModel { DisplayName = "Ghost", TelegramId = 3003 })));
    }

    [Fact]
    public void PutUser_Unknown_Is404()
    {
        Assert.Equal(404, Status(Users().Put(Guid.NewGuid(), new UserCreationModel { DisplayName = "X" })));
    }

    [Fact]
    public void DeleteUser_MapsResults()
    {
        var id = Guid.NewGuid();
        _users.DeleteUser(id).Returns(DeleteUserResult.Deleted, DeleteUserResult.InUse, DeleteUserResult.NotFound);

        Assert.Equal(200, Status(Users().Delete(id)));
        var inUse = Users().Delete(id);
        Assert.Equal(409, Status(inUse));
        Assert.Contains("Merge", Message(inUse));
        Assert.Equal(404, Status(Users().Delete(id)));
    }

    [Fact]
    public void DeleteUser_Self_Is400()
    {
        Assert.Equal(400, Status(Users().Delete(_me)));
        _users.DidNotReceiveWithAnyArgs().DeleteUser(default);
    }

    [Fact]
    public void RequestSubscription_UsesTokenUserAndChecksTheContact()
    {
        var boris = Guid.NewGuid();
        _users.GetUserById(boris).Returns(new UserModel { Id = boris, DisplayName = "Boris" });

        Assert.Equal(200, Status(Contacts().RequestSubscription(boris)));
        _contacts.Received(1).RequestSubscription(_me, boris);

        Assert.Equal(400, Status(Contacts().RequestSubscription(_me)));
        Assert.Equal(404, Status(Contacts().RequestSubscription(Guid.NewGuid())));
        _contacts.Received(1).RequestSubscription(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public void ConfirmAndDecline_UseTokenUser_And404WithoutRequest()
    {
        var boris = Guid.NewGuid();
        _contacts.ConfirmSubscription(_me, boris).Returns(true);
        _contacts.DeclineSubscription(_me, boris).Returns(true);

        Assert.Equal(200, Status(Contacts().ConfirmSubscription(boris)));
        Assert.Equal(200, Status(Contacts().DeclineSubscription(boris)));
        Assert.Equal(404, Status(Contacts().ConfirmSubscription(Guid.NewGuid())));
        Assert.Equal(404, Status(Contacts().DeclineSubscription(Guid.NewGuid())));
    }
}

/// <summary>The bot's /FinalizeBill, /CancelBill and their buttons, and the exchange Finalize button: creator or admin only.</summary>
public class BotCreatorCheckTests
{
    private const long Creator = 1001;
    private const long Other = 1002;

    private readonly IBillService _bills = Substitute.For<IBillService>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();
    private readonly Guid _billId = Guid.NewGuid();

    public BotCreatorCheckTests()
    {
        _bills.CanTelegramUserChangeAsync(Arg.Any<Guid>(), Creator, Arg.Any<CancellationToken>()).Returns(true);
        _bills.CanTelegramUserChangeAsync(Arg.Any<Guid>(), Other, Arg.Any<CancellationToken>()).Returns(false);
        _bills.FinalizeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private static CallbackQuery Button(string data, long from) => new()
    {
        Id = "cb",
        From = new global::Telegram.Bot.Types.User { Id = from, FirstName = "X" },
        Data = data,
        Message = new Message { Id = 7, Chat = new Chat { Id = 555 } },
    };

    private ProcessedMessage Command(long from) => new()
    {
        ChatId = 555,
        FromId = from,
        BotCommand = "/x",
        ProcessedText = _billId.ToString(),
        UserSearchModels = [],
    };

    private void AssertRefusalPublished() =>
        _publish.Received(1).Publish(
            Arg.Is<TelegramMessageRequested>(m => m.ChatId == 555 && m.Text == FinalizeBillCommand.NotAllowedText),
            Arg.Any<CancellationToken>());

    [Fact]
    public async Task FinalizeButton_OtherUser_IsRefused()
    {
        var command = new FinalizeBillCommand(_bills, _publish);

        var answer = await command.ExecuteAsync(Button(FinalizeBillCommand.FormatCallbackData(_billId), Other), default);

        Assert.Equal(FinalizeBillCommand.NotAllowedText, answer);
        await _bills.DidNotReceiveWithAnyArgs().FinalizeAsync(default, default);
    }

    [Fact]
    public async Task FinalizeButton_Creator_Finalizes()
    {
        var command = new FinalizeBillCommand(_bills, _publish);

        var answer = await command.ExecuteAsync(Button(FinalizeBillCommand.FormatCallbackData(_billId), Creator), default);

        Assert.Null(answer);
        await _bills.Received(1).FinalizeAsync(_billId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeCommand_OtherUser_GetsRefusalMessage()
    {
        var command = new FinalizeBillCommand(_bills, _publish);

        await command.ExecuteAsync(Command(Other), Substitute.For<ITelegramBotClient>(), default);

        await _bills.DidNotReceiveWithAnyArgs().FinalizeAsync(default, default);
        AssertRefusalPublished();
    }

    [Fact]
    public async Task FinalizeCommand_Creator_Finalizes()
    {
        var command = new FinalizeBillCommand(_bills, _publish);

        await command.ExecuteAsync(Command(Creator), Substitute.For<ITelegramBotClient>(), default);

        await _bills.Received(1).FinalizeAsync(_billId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelButtonAndCommand_OtherUser_AreRefused()
    {
        var command = new CancelBillCommand(_bills, _publish);

        Assert.Equal(FinalizeBillCommand.NotAllowedText,
            await command.ExecuteAsync(Button(CancelBillCommand.FormatCallbackData(_billId), Other), default));
        await command.ExecuteAsync(Command(Other), Substitute.For<ITelegramBotClient>(), default);

        await _bills.DidNotReceiveWithAnyArgs().CancelAsync(default, default);
        AssertRefusalPublished();
    }

    [Fact]
    public async Task CancelButtonAndCommand_Creator_Cancel()
    {
        var command = new CancelBillCommand(_bills, _publish);

        Assert.Null(await command.ExecuteAsync(Button(CancelBillCommand.FormatCallbackData(_billId), Creator), default));
        await command.ExecuteAsync(Command(Creator), Substitute.For<ITelegramBotClient>(), default);

        await _bills.Received(2).CancelAsync(_billId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExchangeFinalizeButton_NotAllowedOnEitherBill_FinalizesNothing()
    {
        var forward = Guid.NewGuid();
        var backward = Guid.NewGuid();
        _bills.CanTelegramUserChangeAsync(forward, Creator, Arg.Any<CancellationToken>()).Returns(true);
        _bills.CanTelegramUserChangeAsync(backward, Creator, Arg.Any<CancellationToken>()).Returns(false);
        var command = new FinalizeExchangeCommand(_bills, _publish);

        var answer = await command.ExecuteAsync(Button(FinalizeExchangeCommand.FormatCallbackData(forward, backward), Creator), default);

        Assert.Equal(FinalizeBillCommand.NotAllowedText, answer);
        await _bills.DidNotReceiveWithAnyArgs().FinalizeAsync(default, default);
    }
}
