using DebtBot.Models.User;
using DebtBot.Services;

namespace DebtBot.Tests;

public class MergeSuggestionsTests
{
    private static AdminUserModel User(
        string name,
        long? telegramId = null,
        string? userName = null,
        bool botEnabled = false,
        string? login = null,
        bool hasPassword = false,
        int bills = 0,
        DateTime? firstBill = null) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = name,
        TelegramId = telegramId,
        TelegramUserName = userName,
        TelegramBotEnabled = botEnabled,
        Login = login,
        HasPassword = hasPassword,
        BillCount = bills,
        FirstBillDate = firstBill,
    };

    [Theory]
    [InlineData("@Anna", "anna")]
    [InlineData("anna", "anna")]
    [InlineData("  @@ANNA ", "anna")]
    [InlineData("@", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeTelegramUserName(string? input, string? expected)
    {
        Assert.Equal(expected, MergeSuggestions.NormalizeTelegramUserName(input));
    }

    [Fact]
    public void SameTelegramId_IsSuggested()
    {
        var a = User("Anna", telegramId: 42);
        var b = User("Anna (placeholder)", telegramId: 42);
        var c = User("Boris", telegramId: 43);

        var suggestion = Assert.Single(MergeSuggestions.Build([a, b, c]));

        Assert.Equal([MergeReason.SameTelegramId], suggestion.Reasons);
        Assert.Equal(new[] { a.Id, b.Id }.OrderBy(x => x), new[] { suggestion.Keep.Id, suggestion.Merge.Id }.OrderBy(x => x));
    }

    [Fact]
    public void UserNameDifferingInCaseOrAt_IsSuggested()
    {
        var a = User("Anna", userName: "@Anna");
        var b = User("anna", userName: "anna");
        var c = User("ANNA", userName: "@ANNA");

        var suggestions = MergeSuggestions.Build([a, b, c]);

        Assert.Equal(3, suggestions.Count);
        Assert.All(suggestions, s => Assert.Equal([MergeReason.SameTelegramUserName], s.Reasons));
    }

    [Fact]
    public void BothReasons_GiveOnePair()
    {
        var a = User("Anna", telegramId: 42, userName: "@anna");
        var b = User("Anna 2", telegramId: 42, userName: "@Anna");

        var suggestion = Assert.Single(MergeSuggestions.Build([a, b]));

        Assert.Equal([MergeReason.SameTelegramId, MergeReason.SameTelegramUserName], suggestion.Reasons);
    }

    [Fact]
    public void Unrelated_AreNotSuggested()
    {
        Assert.Empty(MergeSuggestions.Build([User("Anna", 1, "@anna"), User("Boris", 2, "@boris"), User("Nobody")]));
    }

    [Fact]
    public void Keeper_TelegramIdWithBotEnabledFirst()
    {
        var placeholder = User("placeholder", userName: "@anna", login: "anna", hasPassword: true, bills: 10);
        var real = User("real", telegramId: 42, userName: "@anna", botEnabled: true);

        var suggestion = Assert.Single(MergeSuggestions.Build([placeholder, real]));

        Assert.Equal(real.Id, suggestion.Keep.Id);
        Assert.Equal(placeholder.Id, suggestion.Merge.Id);
    }

    [Fact]
    public void Keeper_TelegramIdWithoutBotDoesNotCount()
    {
        var noBot = User("no bot", telegramId: 42, userName: "@anna");
        var withLogin = User("login", userName: "@anna", login: "anna");

        Assert.Equal(withLogin.Id, Assert.Single(MergeSuggestions.Build([noBot, withLogin])).Keep.Id);
    }

    [Fact]
    public void Keeper_ThenLoginOrPassword()
    {
        var plain = User("plain", telegramId: 42, botEnabled: true, bills: 5);
        var withPassword = User("password", telegramId: 42, botEnabled: true, hasPassword: true);

        Assert.Equal(withPassword.Id, Assert.Single(MergeSuggestions.Build([plain, withPassword])).Keep.Id);
    }

    [Fact]
    public void Keeper_ThenMoreBills()
    {
        var few = User("few", telegramId: 42, bills: 1);
        var many = User("many", telegramId: 42, bills: 7);

        Assert.Equal(many.Id, Assert.Single(MergeSuggestions.Build([few, many])).Keep.Id);
    }

    [Fact]
    public void Keeper_ThenOlder()
    {
        var newer = User("newer", telegramId: 42, bills: 2, firstBill: new DateTime(2025, 6, 1));
        var older = User("older", telegramId: 42, bills: 2, firstBill: new DateTime(2024, 1, 1));
        var none = User("none", telegramId: 42, bills: 2);

        var suggestions = MergeSuggestions.Build([newer, none, older]);

        Assert.Equal(3, suggestions.Count);
        Assert.Equal(older.Id, suggestions.Single(s => s.Merge.Id == newer.Id).Keep.Id);
        // A user with a first bill date counts as older than one without
        Assert.Equal(older.Id, suggestions.Single(s => s.Keep.Id == older.Id && s.Merge.Id == none.Id).Keep.Id);
        Assert.Single(suggestions, s => s.Keep.Id == newer.Id && s.Merge.Id == none.Id);
    }
}
