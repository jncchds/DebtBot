using AutoMapper;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models.Enums;
using DebtBot.Repositories;
using DebtBot.Services;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace DebtBot.Tests;

/// <summary>SQLite has no rowversion: give <see cref="User.ModifiedAt"/> a default so inserts work.</summary>
public class SqliteDebtContext : DebtContext
{
    public SqliteDebtContext(DbContextOptions<DebtContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>()
            .Property(u => u.ModifiedAt)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("x'00'");
    }
}

/// <summary>
/// The real repositories and services over an in-memory SQLite database. SQLite (unlike EF InMemory) runs
/// transactions, unique indexes and ExecuteUpdate/ExecuteDelete, which the bill and merge code relies on.
/// </summary>
public sealed class SqliteFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public DebtContext Db { get; }
    public IMapper Mapper { get; }
    public IPublishEndpoint Publish { get; } = Substitute.For<IPublishEndpoint>();
    public UserRepository UserRepository { get; }
    public UserContactService UserContactService { get; }
    public UserService UserService { get; }
    public BillRepository BillRepository { get; }
    public BillService BillService { get; }

    public SqliteFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Db = new SqliteDebtContext(new DbContextOptionsBuilder<DebtContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();

        Mapper = new MapperConfiguration(cfg => cfg.AddMaps(typeof(BillService).Assembly)).CreateMapper();

        UserRepository = new UserRepository(Db, Mapper);
        UserContactService = new UserContactService(Db, Mapper);
        UserService = new UserService(UserRepository, Mapper, UserContactService);
        BillRepository = new BillRepository(Db, Mapper);
        BillService = new BillService(Db, Mapper, Publish, UserService, BillRepository);
    }

    public User AddUser(
        string displayName,
        long? telegramId = null,
        string? telegramUserName = null,
        UserRole role = UserRole.Regular,
        string? login = null,
        string? passwordHash = null,
        bool botEnabled = false)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            TelegramId = telegramId,
            TelegramUserName = telegramUserName,
            Role = role,
            Login = login,
            PasswordHash = passwordHash,
            TelegramBotEnabled = botEnabled,
        };
        Db.Users.Add(user);
        Db.SaveChanges();
        return user;
    }

    /// <summary>Forgets tracked entities so the next query reads what is in the database.</summary>
    public void Clear() => Db.ChangeTracker.Clear();

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
