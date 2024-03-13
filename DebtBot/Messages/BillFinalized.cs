namespace DebtBot.Messages;

public record BillFinalized(Guid id, bool forceSponsor = false);