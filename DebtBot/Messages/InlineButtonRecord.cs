namespace DebtBot.Messages;

/// <summary>An inline button: a callback with <paramref name="data"/>, or a link when <paramref name="url"/> is set.</summary>
public record InlineButtonRecord (string text, string data, string? url = null);