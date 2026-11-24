using System.Security.Claims;
using DebtBot.Controllers;
using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;
using DebtBot.Models.Enums;
using DebtBot.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using NSubstitute;

namespace DebtBot.Tests;

/// <summary>T8: <c>POST Users/{id}/Role</c> (not for yourself, Regular removes admin) and <c>GET Bills/All</c> (filters, sort, paging).</summary>
public sealed class AdminTests : IDisposable
{
    private readonly SqliteFixture _f = new();
    private readonly User _admin;
    private readonly User _anna;
    private readonly User _boris;
    private readonly User _clara;

    public AdminTests()
    {
        _admin = _f.AddUser("Admin", role: UserRole.Admin);
        _anna = _f.AddUser("Anna");
        _boris = _f.AddUser("Boris");
        _clara = _f.AddUser("Clara");
    }

    public void Dispose() => _f.Dispose();

    private static DateTime Day(int d) => new(2026, 11, d, 12, 0, 0, DateTimeKind.Utc);

    private Guid AddBill(string description, DateTime date, decimal total, string currency, User creator, User[] people,
        ProcessingState status = ProcessingState.Draft)
    {
        var result = _f.BillService.Create(new BillCreationModel
        {
            CurrencyCode = currency,
            Description = description,
            Date = date,
            Lines =
            [
                new BillLineCreationModel
                {
                    ItemDescription = "Item",
                    Subtotal = total,
                    Participants = people.Select(p => new BillLineParticipantCreationModel { UserId = p.Id }).ToList(),
                },
            ],
            Payments = [new BillPaymentCreationModel { UserId = creator.Id, Amount = total }],
        }, creator.Id);
        Assert.Equal(BillActionStatus.Ok, result.Status);
        _f.Clear();

        // BillService.Create writes the participants itself (creator, line people and payers).
        var bill = _f.Db.Bills.Single(b => b.Id == result.Value);
        bill.Status = status;
        _f.Db.SaveChanges();
        _f.Clear();
        return result.Value;
    }

    private static T As<T>(T controller, User me) where T : ControllerBase
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, me.Id.ToString()) };
        if (me.Role == UserRole.Admin)
            claims.Add(new Claim(DebtBot.Identity.IdentityData.AdminUserClaimName, "true"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) },
        };
        return controller;
    }

    private UsersController Users(User me) => As(new UsersController(_f.UserService, Substitute.For<IAuthService>()), me);

    private BillsController Bills(User me) =>
        As(new BillsController(_f.BillService, Substitute.For<IExcelService>(), _f.UserService, Substitute.For<IParserService>(), _f.UserContactService), me);

    private static int? Status(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode;
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private UserRole RoleOf(User user)
    {
        _f.Clear();
        return _f.Db.Users.Single(u => u.Id == user.Id).Role;
    }

    private List<string> All(AdminBillListQuery q) =>
        Value<PagingResult<BillListModel>>(Bills(_admin).GetAll(q)).Items.Select(b => b.Description).ToList();

    [Fact]
    public void SetRole_MakesAdmin_ThenRegularRemovesIt()
    {
        Assert.Equal(200, Status(Users(_admin).SetRole(_anna.Id, UserRole.Admin)));
        Assert.Equal(UserRole.Admin, RoleOf(_anna));

        Assert.Equal(200, Status(Users(_admin).SetRole(_anna.Id, UserRole.Regular)));
        Assert.Equal(UserRole.Regular, RoleOf(_anna));
    }

    [Fact]
    public void SetRole_RefusesYourOwnRole()
    {
        Assert.Equal(400, Status(Users(_admin).SetRole(_admin.Id, UserRole.Regular)));
        Assert.Equal(UserRole.Admin, RoleOf(_admin));
        Assert.Equal(400, Status(Users(_admin).SetRole(_admin.Id, UserRole.Admin)));
    }

    [Fact]
    public void SetRole_UnknownUser_Is404()
    {
        Assert.Equal(404, Status(Users(_admin).SetRole(Guid.NewGuid(), UserRole.Admin)));
    }

    [Fact]
    public void All_ListsEveryBill_WithParticipantCount_NewestFirst()
    {
        AddBill("Pizza", Day(18), 50m, "EUR", _anna, [_anna, _boris]);
        AddBill("Cinema", Day(8), 24m, "EUR", _boris, [_boris, _clara, _anna], ProcessingState.Cancelled);
        AddBill("Groceries", Day(12), 63.4m, "pln", _clara, [_clara], ProcessingState.Processed);

        var page = Value<PagingResult<BillListModel>>(Bills(_admin).GetAll(new AdminBillListQuery()));
        Assert.Equal(["Pizza", "Groceries", "Cinema"], page.Items.Select(b => b.Description));
        Assert.Equal([2, 1, 3], page.Items.Select(b => b.ParticipantCount));
        Assert.Equal("Anna", page.Items[0].Creator.DisplayName);
    }

    [Fact]
    public void All_FiltersByCreatorPersonCurrencyStatusDatesAndSearch()
    {
        AddBill("Pizza night", Day(18), 50m, "EUR", _anna, [_anna, _boris]);
        AddBill("Cinema", Day(8), 24m, "EUR", _boris, [_boris, _clara], ProcessingState.Cancelled);
        AddBill("Groceries", Day(12), 63.4m, "PLN", _clara, [_clara, _anna], ProcessingState.Processed);

        Assert.Equal(["Cinema"], All(new AdminBillListQuery { CreatorId = _boris.Id }));
        Assert.Equal(["Pizza night", "Cinema"], All(new AdminBillListQuery { WithUserId = _boris.Id }));
        Assert.Equal(["Groceries"], All(new AdminBillListQuery { CreatorId = _clara.Id, WithUserId = _anna.Id }));
        Assert.Equal(["Groceries"], All(new AdminBillListQuery { CurrencyCode = " pln " }));
        Assert.Equal(["Cinema"], All(new AdminBillListQuery { Status = ProcessingState.Cancelled }));
        Assert.Equal(["Pizza night"], All(new AdminBillListQuery { Search = "PIZZA" }));
        Assert.Equal(["Groceries", "Cinema"], All(new AdminBillListQuery { DateTo = new DateTime(2026, 11, 12) }));
        Assert.Equal(["Pizza night", "Groceries"], All(new AdminBillListQuery { DateFrom = new DateTime(2026, 11, 12) }));
        // Bills' own-list switches mean nothing here.
        Assert.Equal(3, All(new AdminBillListQuery { CreatedByMe = true, OfUserId = _anna.Id }).Count);
    }

    [Fact]
    public void All_SortsByEachField_AndPagesStably()
    {
        AddBill("Pizza", Day(18), 50m, "EUR", _anna, [_anna, _boris]);
        AddBill("Cinema", Day(8), 24m, "EUR", _boris, [_boris], ProcessingState.Cancelled);
        AddBill("Groceries", Day(12), 63.4m, "PLN", _clara, [_clara], ProcessingState.Processed);

        Assert.Equal(["Cinema", "Groceries", "Pizza"], All(new AdminBillListQuery { SortOrder = SortOrder.Asc }));
        Assert.Equal(["Cinema", "Groceries", "Pizza"], All(new AdminBillListQuery { SortBy = BillSortField.Description, SortOrder = SortOrder.Asc }));
        Assert.Equal(["Groceries", "Pizza", "Cinema"], All(new AdminBillListQuery { SortBy = BillSortField.Total }));
        Assert.Equal(["Pizza", "Groceries", "Cinema"], All(new AdminBillListQuery { SortBy = BillSortField.Status, SortOrder = SortOrder.Asc }));

        var first = Value<PagingResult<BillListModel>>(Bills(_admin).GetAll(new AdminBillListQuery { CountPerPage = 2 }));
        var second = Value<PagingResult<BillListModel>>(Bills(_admin).GetAll(new AdminBillListQuery { CountPerPage = 2, PageNumber = 1 }));
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(["Pizza", "Groceries"], first.Items.Select(b => b.Description));
        Assert.Equal(["Cinema"], second.Items.Select(b => b.Description));
    }
}
