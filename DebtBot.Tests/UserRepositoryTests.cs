using System.Text.Json;
using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Contact;
using DebtBot.Models.Enums;
using DebtBot.Models.Profile;
using DebtBot.Models.User;

namespace DebtBot.Tests;

/// <summary>Merge, the admin users list, profile and contacts, on the real repository over SQLite.</summary>
public sealed class UserRepositoryTests : IDisposable
{
    private readonly SqliteFixture _f = new();

    public void Dispose() => _f.Dispose();

    private Bill AddBill(User creator, ProcessingState status = ProcessingState.Processed)
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
            Status = status,
        };
        _f.Db.Bills.Add(bill);
        _f.Db.SaveChanges();
        return bill;
    }

    private void AddRefreshToken(User user) =>
        _f.Db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });

    // --- merge ---

    [Fact]
    public void Merge_MovesLoginAndPassword_WhenTheKeptUserHasNone()
    {
        var old = _f.AddUser("Anna (web)", login: "anna", passwordHash: "HASH");
        var kept = _f.AddUser("Anna", telegramId: 42, telegramUserName: "@anna", botEnabled: true);
        AddRefreshToken(old);
        AddRefreshToken(old);
        AddRefreshToken(kept);
        _f.Db.SaveChanges();

        Assert.Equal(MergeUsersResult.Merged, _f.UserService.MergeUsers(old.Id, kept.Id));

        _f.Clear();
        Assert.Null(_f.Db.Users.FirstOrDefault(u => u.Id == old.Id));
        var merged = _f.Db.Users.Single(u => u.Id == kept.Id);
        Assert.Equal("anna", merged.Login);
        Assert.Equal("HASH", merged.PasswordHash);
        Assert.Equal(42, merged.TelegramId);
        Assert.Empty(_f.Db.RefreshTokens.Where(t => t.UserId == old.Id));
        Assert.Single(_f.Db.RefreshTokens.Where(t => t.UserId == kept.Id));
    }

    [Fact]
    public void Merge_KeepsTheKeptUsersLoginAndPassword()
    {
        var old = _f.AddUser("Old", login: "old.login", passwordHash: "OLD");
        var kept = _f.AddUser("Kept", login: "kept.login", passwordHash: "KEPT");

        Assert.Equal(MergeUsersResult.Merged, _f.UserService.MergeUsers(old.Id, kept.Id));

        _f.Clear();
        var merged = _f.Db.Users.Single(u => u.Id == kept.Id);
        Assert.Equal("kept.login", merged.Login);
        Assert.Equal("KEPT", merged.PasswordHash);
        // The old login is free again
        var next = _f.AddUser("Next", login: "old.login");
        Assert.Equal("old.login", next.Login);
    }

    [Fact]
    public void Merge_SameOrUnknownUser()
    {
        var a = _f.AddUser("A");

        Assert.Equal(MergeUsersResult.SameUser, _f.UserService.MergeUsers(a.Id, a.Id));
        Assert.Equal(MergeUsersResult.NotFound, _f.UserService.MergeUsers(Guid.NewGuid(), a.Id));
        Assert.Equal(MergeUsersResult.NotFound, _f.UserService.MergeUsers(a.Id, Guid.NewGuid()));
        Assert.Single(_f.Db.Users);
    }

    [Fact]
    public void Merge_MovesAndCombinesEverythingTheOldUserOwns()
    {
        var old = _f.AddUser("Anna placeholder", telegramUserName: "@Anna");
        var kept = _f.AddUser("Anna", telegramId: 42, telegramUserName: "@anna", botEnabled: true);
        var carl = _f.AddUser("Carl");

        var own = AddBill(old);
        var shared = AddBill(carl);
        var line = new BillLine { Id = Guid.NewGuid(), BillId = shared.Id, ItemDescription = "Pizza", Subtotal = 30m };
        _f.Db.BillLines.Add(line);
        _f.Db.BillPayments.AddRange(
            new BillPayment { BillId = own.Id, UserId = old.Id, Amount = 30m },
            new BillPayment { BillId = shared.Id, UserId = old.Id, Amount = 10m },
            new BillPayment { BillId = shared.Id, UserId = kept.Id, Amount = 5m });
        _f.Db.BillLineParticipants.AddRange(
            new BillLineParticipant { BillLineId = line.Id, UserId = old.Id, Part = 1m },
            new BillLineParticipant { BillLineId = line.Id, UserId = kept.Id, Part = 2m });
        _f.Db.BillParticipants.AddRange(
            new BillParticipant { BillId = own.Id, UserId = old.Id },
            new BillParticipant { BillId = shared.Id, UserId = old.Id },
            new BillParticipant { BillId = shared.Id, UserId = kept.Id });
        _f.Db.Spendings.AddRange(
            new Spending { BillId = shared.Id, UserId = old.Id, Description = "d", Amount = 10m, PaymentAmount = 10m, Portion = 0.25m, CurrencyCode = "EUR", PaymentCurrencyCode = "EUR" },
            new Spending { BillId = shared.Id, UserId = kept.Id, Description = "d", Amount = 20m, PaymentAmount = 20m, Portion = 0.5m, CurrencyCode = "EUR", PaymentCurrencyCode = "EUR" },
            new Spending { BillId = own.Id, UserId = old.Id, Description = "d", Amount = 30m, PaymentAmount = 30m, Portion = 1m, CurrencyCode = "EUR", PaymentCurrencyCode = "EUR" });
        _f.Db.LedgerRecords.AddRange(
            new LedgerRecord { BillId = shared.Id, CreditorUserId = carl.Id, DebtorUserId = old.Id, Amount = 3m, CurrencyCode = "EUR" },
            new LedgerRecord { BillId = shared.Id, CreditorUserId = carl.Id, DebtorUserId = kept.Id, Amount = 4m, CurrencyCode = "EUR" },
            new LedgerRecord { BillId = shared.Id, CreditorUserId = old.Id, DebtorUserId = kept.Id, Amount = 9m, CurrencyCode = "EUR" });
        _f.Db.UserContactsLinks.AddRange(
            new UserContactLink { UserId = old.Id, ContactUserId = carl.Id, DisplayName = "Carl (old)" },
            new UserContactLink { UserId = kept.Id, ContactUserId = carl.Id, DisplayName = "Carl" },
            new UserContactLink { UserId = old.Id, ContactUserId = kept.Id, DisplayName = "me" },
            new UserContactLink { UserId = carl.Id, ContactUserId = old.Id, DisplayName = "Anna" });
        _f.Db.NotificationSubscriptions.AddRange(
            new NotificationSubscription { SubscriberId = carl.Id, UserId = old.Id, IsConfirmed = true },
            new NotificationSubscription { SubscriberId = carl.Id, UserId = kept.Id, IsConfirmed = false },
            new NotificationSubscription { SubscriberId = old.Id, UserId = kept.Id, IsConfirmed = true });
        _f.Db.SaveChanges();

        Assert.Equal(MergeUsersResult.Merged, _f.UserService.MergeUsers(old.Id, kept.Id));
        _f.Clear();

        Assert.Null(_f.Db.Users.FirstOrDefault(u => u.Id == old.Id));

        Assert.Equal(kept.Id, _f.Db.Bills.Single(b => b.Id == own.Id).CreatorId);
        Assert.Equal(30m, _f.Db.BillPayments.Single(p => p.BillId == own.Id).Amount);
        Assert.Equal(kept.Id, _f.Db.BillPayments.Single(p => p.BillId == own.Id).UserId);
        Assert.Equal(15m, _f.Db.BillPayments.Single(p => p.BillId == shared.Id).Amount);
        Assert.Equal(3m, _f.Db.BillLineParticipants.Single(p => p.BillLineId == line.Id).Part);
        Assert.Equal(2, _f.Db.BillParticipants.Count(p => p.UserId == kept.Id));

        var sharedSpending = _f.Db.Spendings.Single(s => s.BillId == shared.Id);
        Assert.Equal((30m, 0.75m), (sharedSpending.Amount, sharedSpending.Portion));
        Assert.Equal(kept.Id, _f.Db.Spendings.Single(s => s.BillId == own.Id).UserId);

        // Carl's two records add up; the old->kept record would be a debt to oneself and is gone
        var ledger = _f.Db.LedgerRecords.Single();
        Assert.Equal((carl.Id, kept.Id, 7m), (ledger.CreditorUserId, ledger.DebtorUserId, ledger.Amount));

        var links = _f.Db.UserContactsLinks.ToList();
        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.UserId == kept.Id && l.ContactUserId == carl.Id && l.DisplayName == "Carl");
        Assert.Contains(links, l => l.UserId == carl.Id && l.ContactUserId == kept.Id);

        var subscription = _f.Db.NotificationSubscriptions.Single();
        Assert.Equal((carl.Id, kept.Id, true), (subscription.SubscriberId, subscription.UserId, subscription.IsConfirmed));

        var merged = _f.Db.Users.Single(u => u.Id == kept.Id);
        Assert.Equal("@anna", merged.TelegramUserName);
    }

    // --- admin users list ---

    [Fact]
    public void AdminUsers_SearchIgnoresCase_AndShowsCounts()
    {
        var anna = _f.AddUser("Anna Smith", telegramUserName: "@anna_s", login: "anna.web", passwordHash: "H");
        var boris = _f.AddUser("Boris", telegramUserName: "@BorisB", role: UserRole.Admin);
        var bill = AddBill(anna);
        _f.Db.BillParticipants.Add(new BillParticipant { BillId = bill.Id, UserId = anna.Id });
        _f.Db.UserContactsLinks.Add(new UserContactLink { UserId = anna.Id, ContactUserId = boris.Id, DisplayName = "B" });
        _f.Db.SaveChanges();

        Assert.Equal([anna.Id], _f.UserService.GetAdminUsers("SMITH", null).Items.Select(u => u.Id));
        Assert.Equal([boris.Id], _f.UserService.GetAdminUsers("borisb", null).Items.Select(u => u.Id));
        Assert.Equal([anna.Id], _f.UserService.GetAdminUsers("ANNA.WEB", null).Items.Select(u => u.Id));
        Assert.Equal([boris.Id], _f.UserService.GetAdminUsers(null, UserRole.Admin).Items.Select(u => u.Id));
        Assert.Equal(2, _f.UserService.GetAdminUsers(null, null).TotalCount);

        var row = _f.UserService.GetAdminUsers("anna", null).Items.Single();
        Assert.True(row.HasPassword);
        Assert.Equal("anna.web", row.Login);
        Assert.Equal(1, row.BillCount);
        Assert.Equal(1, row.ContactCount);
        Assert.Equal(bill.Date, row.FirstBillDate);
        Assert.False(_f.UserService.GetAdminUsers("boris", null).Items.Single().HasPassword);
    }

    [Fact]
    public void AdminUsers_Paging()
    {
        for (var i = 0; i < 5; i++)
            _f.AddUser($"User {i}");

        var page = _f.UserService.GetAdminUsers(null, null, pageNumber: 1, countPerPage: 2);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(["User 2", "User 3"], page.Items.Select(u => u.DisplayName));
    }

    [Fact]
    public void AdminUserModel_HasNoPasswordHash()
    {
        Assert.DoesNotContain(typeof(AdminUserModel).GetProperties(), p => p.Name.Contains("Hash"));
        Assert.DoesNotContain(typeof(ProfileModel).GetProperties(), p => p.Name.Contains("Hash"));
        Assert.DoesNotContain(typeof(UserModel).GetProperties(), p => p.Name.Contains("Hash"));
    }

    [Fact]
    public void MergeSuggestions_FromTheDatabase()
    {
        var real = _f.AddUser("Anna", telegramId: 42, telegramUserName: "@anna", botEnabled: true);
        var placeholder = _f.AddUser("@Anna", telegramUserName: "@ANNA");
        _f.AddUser("Boris", telegramId: 43, telegramUserName: "@boris");

        var suggestion = Assert.Single(_f.UserService.GetMergeSuggestions());

        Assert.Equal(real.Id, suggestion.Keep.Id);
        Assert.Equal(placeholder.Id, suggestion.Merge.Id);
        Assert.Equal([MergeReason.SameTelegramUserName], suggestion.Reasons);
    }

    // --- profile ---

    [Fact]
    public void ProfilePut_ChangesOnlyNamePhoneAndEmail()
    {
        var user = _f.AddUser("Anna", telegramId: 42, telegramUserName: "@anna", login: "anna", passwordHash: "H", botEnabled: true);

        // A client sending Telegram fields: they aren't part of the model, so they're dropped
        var model = JsonSerializer.Deserialize<ProfileUpdateModel>(
            """{"displayName":" Anna S ","phone":"+48 123","email":"anna@example.com","telegramId":999,"telegramUserName":"@evil","login":"evil","role":"Admin"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Empty(_f.UserService.UpdateProfile(user.Id, model));

        _f.Clear();
        var saved = _f.Db.Users.Single(u => u.Id == user.Id);
        Assert.Equal("Anna S", saved.DisplayName);
        Assert.Equal("+48 123", saved.Phone);
        Assert.Equal("anna@example.com", saved.Email);
        Assert.Equal(42, saved.TelegramId);
        Assert.Equal("@anna", saved.TelegramUserName);
        Assert.Equal("anna", saved.Login);
        Assert.Equal(UserRole.Regular, saved.Role);

        var profile = _f.UserService.GetProfile(user.Id)!;
        Assert.True(profile.HasPassword);
        Assert.True(profile.TelegramBotEnabled);
        Assert.Equal(42, profile.TelegramId);
    }

    [Fact]
    public void ProfilePut_ValidatesAndClears()
    {
        var user = _f.AddUser("Anna");
        _f.UserService.UpdateProfile(user.Id, new ProfileUpdateModel { DisplayName = "Anna", Phone = "1", Email = "a@b" });

        Assert.NotEmpty(_f.UserService.UpdateProfile(user.Id, new ProfileUpdateModel { DisplayName = "  " }));
        Assert.NotEmpty(_f.UserService.UpdateProfile(user.Id, new ProfileUpdateModel { DisplayName = "Anna", Email = "nope" }));
        Assert.NotEmpty(_f.UserService.UpdateProfile(user.Id, new ProfileUpdateModel { DisplayName = new string('x', 101) }));

        Assert.Empty(_f.UserService.UpdateProfile(user.Id, new ProfileUpdateModel { DisplayName = "Anna", Phone = "", Email = null }));
        _f.Clear();
        var saved = _f.Db.Users.Single(u => u.Id == user.Id);
        Assert.Null(saved.Phone);
        Assert.Null(saved.Email);
    }

    // --- contacts ---

    [Fact]
    public void Contacts_EmptyListRenameRemoveAndSubscriptions()
    {
        var anna = _f.AddUser("Anna");
        var boris = _f.AddUser("Boris", telegramUserName: "@boris", botEnabled: true);

        Assert.Empty(_f.UserContactService.GetContacts(anna.Id));

        _f.UserContactService.EnsureContact(anna.Id, boris.Id, "Boris");
        _f.UserContactService.EnsureContact(anna.Id, boris.Id, "ignored");
        _f.UserContactService.RequestSubscription(anna.Id, boris.Id);
        _f.UserContactService.RequestSubscription(boris.Id, anna.Id);
        _f.UserContactService.ConfirmSubscription(anna.Id, boris.Id);

        var contact = Assert.Single(_f.UserContactService.GetContacts(anna.Id));
        Assert.Equal(("Boris", "Boris", "@boris", true), (contact.DisplayName, contact.UserDisplayName, contact.TelegramUserName, contact.TelegramBotEnabled));
        Assert.Equal(SubscriptionState.Pending, contact.MySubscription);
        Assert.Equal(SubscriptionState.Confirmed, contact.TheirSubscription);

        Assert.True(_f.UserContactService.RenameContact(anna.Id, boris.Id, "Bob"));
        Assert.Equal("Bob", _f.UserContactService.GetContact(anna.Id, boris.Id)!.DisplayName);
        Assert.False(_f.UserContactService.RenameContact(boris.Id, anna.Id, "x"));

        Assert.True(_f.UserContactService.RemoveContact(anna.Id, boris.Id));
        Assert.False(_f.UserContactService.RemoveContact(anna.Id, boris.Id));
        Assert.Empty(_f.UserContactService.GetContacts(anna.Id));
    }
}
