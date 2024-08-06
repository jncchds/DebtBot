namespace DebtBot.Models.Enums;

public enum ProcessingState : byte
{
    Draft = 0,
    Ready = 1,
    Processed = 3,
    Cancelled = 4
}
