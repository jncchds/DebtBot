namespace DebtBot.Messages;

public record SendBillMessage (Guid BillId, long ChatId);
