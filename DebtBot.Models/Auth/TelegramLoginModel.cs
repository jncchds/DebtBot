using System.Text.Json.Serialization;

namespace DebtBot.Models.Auth;

/// <summary>
/// The payload the Telegram Login Widget hands to its callback, with the widget's snake_case names.
/// Optional fields that Telegram didn't send must stay null: they are not part of the signed data.
/// </summary>
public class TelegramLoginModel
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("photo_url")]
    public string? PhotoUrl { get; set; }

    [JsonPropertyName("auth_date")]
    public long AuthDate { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }
}
