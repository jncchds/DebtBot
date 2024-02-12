namespace DebtBot.Messages;

public record LedgerRecordCreated(
	Guid CreditorUserId,
	Guid DebtorUserId,
	decimal Amount,
	string CurrencyCode);