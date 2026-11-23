using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using DebtBot.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DebtBot.Tests;

public class ManualTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>An <see cref="AuthService"/> over an in-memory database, with the real hasher and token code.</summary>
public sealed class AuthServiceFixture : IDisposable
{
    public const string BotToken = "123456789:AAFakeBotTokenForTestsOnly_xyz";

    public DebtContext Db { get; }
    public IUserService UserService { get; } = Substitute.For<IUserService>();
    public ManualTimeProvider Time { get; } = new();
    public DebtBotConfiguration Config { get; } = new()
    {
        JwtConfiguration = new JwtConfiguration
        {
            Issuer = "DebtBot",
            Audience = "DebtBot",
            Key = "TestsOnlyKeyTestsOnlyKeyTestsOnlyKeyTestsOnlyKey",
            LifeTime = 60,
            RefreshTokenLifetimeDays = 30,
        },
        Telegram = new TelegramConfiguration { BotToken = BotToken },
    };
    public AuthService Service { get; }

    public AuthServiceFixture()
    {
        var options = new DbContextOptionsBuilder<DebtContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), o => o.EnableNullChecks(false))
            .Options;
        Db = new DebtContext(options);

        UserService.GetUserById(Arg.Any<Guid>()).Returns(call =>
        {
            var entity = Db.Users.AsNoTracking().FirstOrDefault(u => u.Id == call.Arg<Guid>());
            return entity is null ? null : ToModel(entity);
        });

        var configOptions = Options.Create(Config);
        Service = new AuthService(
            Db,
            UserService,
            new IdentityService(configOptions),
            new PasswordHasher<User>(),
            new LoginThrottle(new MemoryCache(new MemoryCacheOptions())),
            Time,
            configOptions,
            NullLogger<AuthService>.Instance);
    }

    public User AddUser(string displayName = "Anna", string? login = null, UserRole role = UserRole.Regular)
    {
        var user = new User { Id = Guid.NewGuid(), DisplayName = displayName, Login = login, Role = role, TelegramId = Random.Shared.NextInt64(1, long.MaxValue) };
        Db.Users.Add(user);
        Db.SaveChanges();
        return user;
    }

    public User Reload(Guid id)
    {
        Db.ChangeTracker.Clear();
        return Db.Users.Single(u => u.Id == id);
    }

    public static UserModel ToModel(User entity) => new()
    {
        Id = entity.Id,
        DisplayName = entity.DisplayName,
        TelegramId = entity.TelegramId,
        TelegramUserName = entity.TelegramUserName,
        Role = entity.Role,
    };

    public void Dispose() => Db.Dispose();
}
