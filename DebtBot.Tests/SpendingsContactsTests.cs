using System.Security.Claims;
using DebtBot.Controllers;
using DebtBot.DB.Entities;
using DebtBot.Models;
using DebtBot.Models.Contact;
using DebtBot.Models.Enums;
using DebtBot.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace DebtBot.Tests;

/// <summary>T7: spending filters, sort and summary; contacts' added date, by-telegram usernames and cancelling your own subscription.</summary>
public sealed class SpendingsContactsTests : IDisposable
{
    private readonly SqliteFixture _f = new();
    private readonly User _anna;
    private readonly User _boris;
    private readonly BudgetService _budget;

    public SpendingsContactsTests()
    {
        _anna = _f.AddUser("Anna", telegramId: 1001, telegramUserName: "@anna_p", botEnabled: true);
        _boris = _f.AddUser("Boris", telegramId: 1002, telegramUserName: "@boris_iv", botEnabled: true);
        _budget = new BudgetService(_f.Db, _f.Mapper);
    }

    public void Dispose() => _f.Dispose();

    private static DateTime Utc(int month, int day, int hour = 12) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    private Guid AddSpending(User user, string description, decimal amount, string currency, DateTime date,
        bool cancelled = false, string? paymentCurrency = null)
    {
        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            CreatorId = user.Id,
            CurrencyCode = currency,
            PaymentCurrencyCode = paymentCurrency ?? currency,
            Description = description,
            Date = date,
            TotalWithTips = amount * 2,
            Status = cancelled ? ProcessingState.Cancelled : ProcessingState.Processed,
        };
        _f.Db.Bills.Add(bill);
        _f.Db.Spendings.Add(new Spending
        {
            BillId = bill.Id,
            UserId = user.Id,
            Description = description,
            Amount = amount,
            CurrencyCode = currency,
            PaymentAmount = amount,
            PaymentCurrencyCode = paymentCurrency ?? currency,
            Portion = 0.5m,
            Date = date,
            IsCanceled = cancelled,
        });
        _f.Db.SaveChanges();
        _f.Clear();
        return bill.Id;
    }

    private void Seed()
    {
        AddSpending(_anna, "Pizza", 12m, "EUR", Utc(11, 3));
        AddSpending(_anna, "Taxi", 30.5m, "EUR", Utc(11, 20));
        AddSpending(_anna, "Market", 100m, "PLN", Utc(11, 21));
        AddSpending(_anna, "Hotel", 200m, "EUR", Utc(10, 30));
        AddSpending(_anna, "Cancelled dinner", 50m, "EUR", Utc(11, 10), cancelled: true);
        AddSpending(_boris, "Not mine", 9m, "EUR", Utc(11, 11));
    }

    [Fact]
    public void Spendings_DefaultIsOwnNewestFirstWithoutCancelled()
    {
        Seed();

        var page = _budget.GetSpendings(_anna.Id, new SpendingListQuery());

        Assert.Equal(["Market", "Taxi", "Pizza", "Hotel"], page.Items.Select(s => s.Description));
        Assert.Equal(4, page.TotalCount);
    }

    [Fact]
    public void Spendings_Filters()
    {
        Seed();

        Assert.Equal(["Taxi", "Cancelled dinner", "Pizza", "Hotel"],
            _budget.GetSpendings(_anna.Id, new SpendingListQuery { CurrencyCode = "EUR", IncludeCancelled = true }).Items.Select(s => s.Description));
        // Whole days: the 20th is included up to its end; a date without a kind (as bound from the query string) works.
        Assert.Equal(["Taxi", "Pizza"], _budget.GetSpendings(_anna.Id, new SpendingListQuery
        {
            DateFrom = new DateTime(2026, 11, 1),
            DateTo = new DateTime(2026, 11, 20),
        }).Items.Select(s => s.Description));
        Assert.Empty(_budget.GetSpendings(_anna.Id, new SpendingListQuery { CurrencyCode = "USD" }).Items);
    }

    [Fact]
    public void Spendings_SortByAmountAndStablePaging()
    {
        Seed();
        AddSpending(_anna, "Tie A", 12m, "EUR", Utc(11, 3));

        var asc = _budget.GetSpendings(_anna.Id, new SpendingListQuery { SortBy = SpendingSortField.Amount, SortOrder = SortOrder.Asc }).Items;
        Assert.Equal([12m, 12m, 30.5m, 100m, 200m], asc.Select(s => s.Amount));
        var desc = _budget.GetSpendings(_anna.Id, new SpendingListQuery { SortBy = SpendingSortField.Amount }).Items;
        Assert.Equal("Hotel", desc.First().Description);

        var pages = Enumerable.Range(0, 3)
            .SelectMany(p => _budget.GetSpendings(_anna.Id, new SpendingListQuery
            {
                SortBy = SpendingSortField.Amount, SortOrder = SortOrder.Asc, PageNumber = p, CountPerPage = 2,
            }).Items)
            .Select(s => s.BillId)
            .ToList();
        Assert.Equal(asc.Select(s => s.BillId), pages);
    }

    [Fact]
    public void Summary_PerCurrencyWithoutCancelled()
    {
        Seed();

        var month = _budget.GetSummary(_anna.Id, new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));
        Assert.Equal(
            [("EUR", 42.5m, 2), ("PLN", 100m, 1)],
            month.Select(s => (s.CurrencyCode, s.Amount, s.Count)));

        var all = _budget.GetSummary(_anna.Id, null, null);
        Assert.Equal((242.5m, 3), (all[0].Amount, all[0].Count));
        Assert.Empty(_budget.GetSummary(_anna.Id, new DateTime(2026, 12, 1), null));
    }

    private UserContactsController Contacts(User me)
    {
        var controller = new UserContactsController(_f.UserContactService, _f.UserService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, me.Id.ToString())], "test")),
                },
            },
        };
        return controller;
    }

    private static int? Status(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode;

    [Theory]
    [InlineData("boris_iv")]
    [InlineData("@boris_iv")]
    [InlineData("  @Boris_IV ")]
    public void ByTelegram_MatchesStoredAtNameWithOrWithoutAt(string username)
    {
        var result = Contacts(_anna).AddOwnByTelegram(username);

        var contact = Assert.IsType<ContactModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(_boris.Id, contact.Id);
        Assert.Equal(2, _f.Db.Users.Count());
    }

    [Fact]
    public void ByTelegram_UnknownMakesPlaceholderWithAddedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = Contacts(_anna).AddOwnByTelegram("new_person");

        var contact = Assert.IsType<ContactModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("@new_person", contact.TelegramUserName);
        Assert.False(contact.TelegramBotEnabled);
        Assert.NotNull(contact.AddedAt);
        Assert.InRange(contact.AddedAt!.Value, before, DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(400, Status(Contacts(_anna).AddOwnByTelegram("ab")));
    }

    [Fact]
    public void AddedAt_NullForOldLinks_KeptWhenEnsuredAgain()
    {
        _f.Db.UserContactsLinks.Add(new UserContactLink { UserId = _anna.Id, ContactUserId = _boris.Id, DisplayName = "B" });
        _f.Db.SaveChanges();
        _f.Clear();

        _f.UserContactService.EnsureContact(_anna.Id, _boris.Id, "Boris");

        Assert.Null(_f.UserContactService.GetContact(_anna.Id, _boris.Id)!.AddedAt);

        _f.UserContactService.EnsureContact(_boris.Id, _anna.Id, "Anna");
        Assert.NotNull(_f.UserContactService.GetContact(_boris.Id, _anna.Id)!.AddedAt);
    }

    [Fact]
    public void CancelSubscription_RemovesOnlyMineAndIs404WhenNone()
    {
        var clara = _f.AddUser("Clara");
        _f.UserContactService.RequestSubscription(_anna.Id, _boris.Id);          // Anna asks for Boris's bills: pending
        _f.UserContactService.RequestSubscription(_anna.Id, clara.Id, true);     // Anna gets Clara's bills: confirmed
        _f.UserContactService.RequestSubscription(_boris.Id, _anna.Id);          // Boris asks for Anna's bills
        _f.Clear();

        Assert.Equal(200, Status(Contacts(_anna).CancelSubscription(_boris.Id)));
        Assert.Equal(200, Status(Contacts(_anna).CancelSubscription(clara.Id)));
        Assert.Equal(404, Status(Contacts(_anna).CancelSubscription(_boris.Id)));

        var left = Assert.Single(_f.Db.NotificationSubscriptions.ToList());
        Assert.Equal((_boris.Id, _anna.Id), (left.SubscriberId, left.UserId));
    }
}
