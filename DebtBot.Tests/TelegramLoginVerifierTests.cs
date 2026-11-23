using DebtBot.Models.Auth;
using DebtBot.Services;
using System.Security.Cryptography;
using System.Text;

namespace DebtBot.Tests;

public class TelegramLoginVerifierTests
{
    private const string BotToken = "123456789:AAFakeBotTokenForTestsOnly_xyz";
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Signs the payload independently of the verifier, the way Telegram does.</summary>
    private static string Sign(params (string Key, string Value)[] fields)
    {
        var dataCheckString = string.Join("\n", fields.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}={f.Value}"));
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(BotToken));
        return Convert.ToHexString(HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();
    }

    private static TelegramLoginModel FullPayload()
    {
        var authDate = Now.AddMinutes(-5).ToUnixTimeSeconds();
        return new TelegramLoginModel
        {
            Id = 42424242,
            FirstName = "Anna",
            LastName = "Ivanova",
            Username = "anna_iv",
            PhotoUrl = "https://t.me/i/userpic/320/anna.jpg",
            AuthDate = authDate,
            Hash = Sign(
                ("id", "42424242"),
                ("first_name", "Anna"),
                ("last_name", "Ivanova"),
                ("username", "anna_iv"),
                ("photo_url", "https://t.me/i/userpic/320/anna.jpg"),
                ("auth_date", authDate.ToString())),
        };
    }

    [Fact]
    public void KnownGoodPayload_IsValid()
    {
        Assert.Equal(TelegramLoginCheck.Valid, TelegramLoginVerifier.Verify(FullPayload(), BotToken, Now));
    }

    [Fact]
    public void DataCheckString_IsSortedAndSkipsHash()
    {
        var payload = FullPayload();
        Assert.Equal(
            $"auth_date={payload.AuthDate}\nfirst_name=Anna\nid=42424242\nlast_name=Ivanova\nphoto_url=https://t.me/i/userpic/320/anna.jpg\nusername=anna_iv",
            TelegramLoginVerifier.BuildDataCheckString(payload));
    }

    [Fact]
    public void PayloadWithoutOptionalFields_IsValid()
    {
        var authDate = Now.AddHours(-1).ToUnixTimeSeconds();
        var payload = new TelegramLoginModel
        {
            Id = 777,
            FirstName = "Boris",
            AuthDate = authDate,
            Hash = Sign(("id", "777"), ("first_name", "Boris"), ("auth_date", authDate.ToString())),
        };

        Assert.Equal(TelegramLoginCheck.Valid, TelegramLoginVerifier.Verify(payload, BotToken, Now));
    }

    [Fact]
    public void OptionalFieldSignedAsEmpty_DoesNotMatchMissingField()
    {
        // Signed without last_name; the payload claims an empty last_name. Must not verify.
        var authDate = Now.AddHours(-1).ToUnixTimeSeconds();
        var payload = new TelegramLoginModel
        {
            Id = 777,
            FirstName = "Boris",
            LastName = "",
            AuthDate = authDate,
            Hash = Sign(("id", "777"), ("first_name", "Boris"), ("auth_date", authDate.ToString())),
        };

        Assert.Equal(TelegramLoginCheck.InvalidHash, TelegramLoginVerifier.Verify(payload, BotToken, Now));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("first_name")]
    [InlineData("username")]
    [InlineData("photo_url")]
    [InlineData("auth_date")]
    public void TamperedField_IsRejected(string field)
    {
        var payload = FullPayload();
        switch (field)
        {
            case "id": payload.Id = 1; break;
            case "first_name": payload.FirstName = "Mallory"; break;
            case "username": payload.Username = "admin"; break;
            case "photo_url": payload.PhotoUrl = null; break;
            case "auth_date": payload.AuthDate += 60; break;
        }

        Assert.Equal(TelegramLoginCheck.InvalidHash, TelegramLoginVerifier.Verify(payload, BotToken, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-hex")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("zz00000000000000000000000000000000000000000000000000000000000000")]
    public void WrongHash_IsRejected(string? hash)
    {
        var payload = FullPayload();
        payload.Hash = hash;

        Assert.Equal(TelegramLoginCheck.InvalidHash, TelegramLoginVerifier.Verify(payload, BotToken, Now));
    }

    [Fact]
    public void HashFromAnotherBotToken_IsRejected()
    {
        var payload = FullPayload();
        payload.Hash = TelegramLoginVerifier.ComputeHash(payload, "987654321:SomeOtherBot");

        Assert.Equal(TelegramLoginCheck.InvalidHash, TelegramLoginVerifier.Verify(payload, BotToken, Now));
    }

    [Fact]
    public void UppercaseHash_IsAccepted()
    {
        var payload = FullPayload();
        payload.Hash = payload.Hash!.ToUpperInvariant();

        Assert.Equal(TelegramLoginCheck.Valid, TelegramLoginVerifier.Verify(payload, BotToken, Now));
    }

    [Fact]
    public void ExpiredAuthDate_IsRejected()
    {
        var authDate = Now.AddHours(-24).AddMinutes(-1).ToUnixTimeSeconds();
        var payload = new TelegramLoginModel
        {
            Id = 777,
            FirstName = "Boris",
            AuthDate = authDate,
            Hash = Sign(("id", "777"), ("first_name", "Boris"), ("auth_date", authDate.ToString())),
        };

        Assert.Equal(TelegramLoginCheck.Expired, TelegramLoginVerifier.Verify(payload, BotToken, Now));
    }

    [Fact]
    public void AuthDateFarInTheFuture_IsRejected()
    {
        var authDate = Now.AddHours(1).ToUnixTimeSeconds();
        var payload = new TelegramLoginModel
        {
            Id = 777,
            FirstName = "Boris",
            AuthDate = authDate,
            Hash = Sign(("id", "777"), ("first_name", "Boris"), ("auth_date", authDate.ToString())),
        };

        Assert.Equal(TelegramLoginCheck.Expired, TelegramLoginVerifier.Verify(payload, BotToken, Now));
    }
}
