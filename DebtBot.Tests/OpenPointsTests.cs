using DebtBot.DB.Entities;
using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;
using DebtBot.Models;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
using DebtBot.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DebtBot.Tests;

/// <summary>T9's open points: bill participants and paging, deleting admins, the refresh-token purge.</summary>
public sealed class OpenPointsTests : IDisposable
{
    private readonly SqliteFixture _f = new();
    private readonly User _anna;
    private readonly User _boris;
    private readonly User _clara;

    public OpenPointsTests()
    {
        _anna = _f.AddUser("Anna");
        _boris = _f.AddUser("Boris");
        _clara = _f.AddUser("Clara");
    }

    public void Dispose() => _f.Dispose();

    private static BillListQuery Query(ProcessingState? status = null) => new() { Status = status, PageNumber = 0, CountPerPage = 20 };

    [Fact]
    public void CreatedBill_IsInEveryParticipantsList_RightAway()
    {
        // The participants used to be written later by a consumer, so GET Bills could miss a bill just created.
        var result = _f.BillService.Create(new BillCreationModel
        {
            CurrencyCode = "EUR",
            Description = "Dinner",
            Lines =
            [
                new BillLineCreationModel
                {
                    ItemDescription = "Pizza",
                    Subtotal = 30m,
                    Participants = [new BillLineParticipantCreationModel { UserId = _boris.Id }],
                },
            ],
            Payments = [new BillPaymentCreationModel { UserId = _clara.Id, Amount = 30m }],
        }, _anna.Id);
        Assert.Equal(BillActionStatus.Ok, result.Status);
        _f.Clear();

        foreach (var user in new[] { _anna, _boris, _clara })
        {
            var page = _f.BillRepository.GetForUser(user.Id, Query());
            Assert.Equal(result.Value, Assert.Single(page.Items).Id);
            Assert.Equal(1, page.TotalCount);
        }
    }

    [Fact]
    public void CreatedExchange_ListsBothDrafts_RightAway()
    {
        var result = _f.BillService.CreateExchange(new ExchangeCreationModel
        {
            FromUserId = _anna.Id, FromAmount = 100m, FromCurrencyCode = "EUR",
            ToUserId = _boris.Id, ToAmount = 430m, ToCurrencyCode = "PLN",
            Date = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc),
        }, _anna.Id);
        Assert.Equal(BillActionStatus.Ok, result.Status);
        _f.Clear();

        var drafts = _f.BillRepository.GetForUser(_anna.Id, Query(ProcessingState.Draft));
        Assert.Equal(2, drafts.TotalCount);
        Assert.Equal(
            new[] { result.Value!.ForwardBillId, result.Value.BackwardBillId }.Order(),
            drafts.Items.Select(b => b.Id).Order());
    }

    [Theory]
    [InlineData(0, 2, 2, 5)]
    [InlineData(1, 2, 2, 5)]
    [InlineData(2, 2, 1, 5)]
    [InlineData(4, 2, 0, 5)]
    [InlineData(0, 10, 5, 5)]
    public void ToPagingResult_PageAndCountAgree(int page, int size, int expectedItems, int expectedTotal)
    {
        var result = Enumerable.Range(1, 5).AsQueryable().ToPagingResult(page, size);

        Assert.Equal(expectedItems, result.Items.Count);
        Assert.Equal(expectedTotal, result.TotalCount);
        Assert.Equal(size, result.CountPerPage);
    }

    [Fact]
    public void ToPagingResult_WithoutPageSize_IsOneConsistentList()
    {
        var result = Enumerable.Range(1, 5).AsQueryable().ToPagingResult();

        Assert.Equal(5, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.True(result.IsLastPage);
    }

    [Fact]
    public void ToPagingResult_RowsAddedBetweenPageAndCount_NeverReportFewerThanShown()
    {
        // The source grows by one every time it's read: the page is read first, then the count.
        var source = new GrowingSource(4);

        var result = source.AsQueryable().ToPagingResult(0, 2);

        Assert.Equal(2, result.Items.Count);
        Assert.True(result.TotalCount >= result.Items.Count);
    }

    [Fact]
    public void DeleteUser_Admin_IsRefused_EvenWithNoData()
    {
        var admin = _f.AddUser("Dmitry", role: UserRole.Admin);
        _f.Clear();

        Assert.Equal(DeleteUserResult.IsAdmin, _f.UserService.DeleteUser(admin.Id));
        _f.Clear();
        Assert.NotNull(_f.Db.Users.Find(admin.Id));
    }

    [Fact]
    public async Task PurgeRefreshTokens_RemovesExpiredAndLongRevoked_KeepsTheRest()
    {
        var time = new ManualTimeProvider();
        var now = time.GetUtcNow().UtcDateTime;
        RefreshToken Token(string hash, DateTime expires, DateTime? revoked = null) => new()
        {
            Id = Guid.NewGuid(), UserId = _anna.Id, TokenHash = hash, CreatedAt = now.AddDays(-20), ExpiresAt = expires, RevokedAt = revoked,
        };
        _f.Db.RefreshTokens.AddRange(
            Token("active", now.AddDays(10)),
            Token("expired", now.AddMinutes(-1)),
            Token("revoked-long-ago", now.AddDays(10), now - AuthService.RevokedTokenRetention - TimeSpan.FromHours(1)),
            Token("revoked-recently", now.AddDays(10), now.AddDays(-1)));
        _f.Db.SaveChanges();
        _f.Clear();

        var config = Options.Create(new DebtBotConfiguration
        {
            JwtConfiguration = new JwtConfiguration { Issuer = "DebtBot", Audience = "DebtBot", Key = "TestsOnlyKeyTestsOnlyKeyTestsOnlyKeyTestsOnlyKey", LifeTime = 60 },
            Telegram = new TelegramConfiguration(),
        });
        var auth = new AuthService(_f.Db, _f.UserService, new IdentityService(config), new PasswordHasher<User>(),
            new LoginThrottle(new MemoryCache(new MemoryCacheOptions())), time, config, NullLogger<AuthService>.Instance);

        Assert.Equal(2, await auth.PurgeRefreshTokensAsync());
        Assert.Equal(new[] { "active", "revoked-recently" }, _f.Db.RefreshTokens.Select(t => t.TokenHash).OrderBy(h => h).ToArray());
        Assert.Equal(0, await auth.PurgeRefreshTokensAsync());
    }

    private sealed class GrowingSource(int start) : IEnumerable<int>
    {
        private int _count = start;

        public IEnumerator<int> GetEnumerator()
        {
            var n = _count++;
            return Enumerable.Range(1, n).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
