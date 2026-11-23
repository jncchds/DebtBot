using DebtBot.Models.Auth;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DebtBot.Services;

public enum TelegramLoginCheck
{
    Valid,
    InvalidHash,
    Expired,
}

/// <summary>
/// Checks the data the Telegram Login Widget sends, as described at https://core.telegram.org/widgets/login#checking-authorization
/// </summary>
public static class TelegramLoginVerifier
{
    /// <summary>How old <c>auth_date</c> may be.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    /// <summary>How far in the future <c>auth_date</c> may be, to allow for clock skew.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The sorted <c>key=value</c> lines joined with <c>\n</c>, built from the fields present in the payload, without <c>hash</c>.
    /// </summary>
    public static string BuildDataCheckString(TelegramLoginModel model)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["id"] = model.Id.ToString(CultureInfo.InvariantCulture),
            ["auth_date"] = model.AuthDate.ToString(CultureInfo.InvariantCulture),
        };
        if (model.FirstName is not null) fields["first_name"] = model.FirstName;
        if (model.LastName is not null) fields["last_name"] = model.LastName;
        if (model.Username is not null) fields["username"] = model.Username;
        if (model.PhotoUrl is not null) fields["photo_url"] = model.PhotoUrl;

        return string.Join("\n", fields.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    /// <summary>HMAC-SHA256 of the data-check-string keyed with SHA256(botToken), as lowercase hex.</summary>
    public static string ComputeHash(TelegramLoginModel model, string botToken)
    {
        return Convert.ToHexString(ComputeHashBytes(model, botToken)).ToLowerInvariant();
    }

    public static TelegramLoginCheck Verify(TelegramLoginModel model, string botToken, DateTimeOffset now)
    {
        if (model.Id == 0 || string.IsNullOrEmpty(model.Hash) || model.Hash.Length != 64)
        {
            return TelegramLoginCheck.InvalidHash;
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(model.Hash);
        }
        catch (FormatException)
        {
            return TelegramLoginCheck.InvalidHash;
        }

        var computed = ComputeHashBytes(model, botToken);
        if (!CryptographicOperations.FixedTimeEquals(computed, expected))
        {
            return TelegramLoginCheck.InvalidHash;
        }

        var authDate = DateTimeOffset.FromUnixTimeSeconds(model.AuthDate);
        if (now - authDate > MaxAge || authDate - now > MaxClockSkew)
        {
            return TelegramLoginCheck.Expired;
        }

        return TelegramLoginCheck.Valid;
    }

    private static byte[] ComputeHashBytes(TelegramLoginModel model, string botToken)
    {
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        return HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(BuildDataCheckString(model)));
    }
}
