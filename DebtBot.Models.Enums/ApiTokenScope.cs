namespace DebtBot.Models.Enums;

/// <summary>What a personal API token may do. A <see cref="Read"/> token gets 403 on anything but GET, HEAD and OPTIONS.</summary>
public enum ApiTokenScope : byte
{
	Read = 0,
	ReadWrite = 1
}
