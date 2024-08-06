using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Models.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Consumers;

public class BillCanceledConsumer : IConsumer<BillCanceled>
{
    private readonly DebtContext _debtContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<BillFinalizedConsumer> _logger;

    public BillCanceledConsumer(DebtContext debtContext, IPublishEndpoint publishEndpoint, ILogger<BillFinalizedConsumer> logger)
    {
        _debtContext = debtContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BillCanceled> context)
    {
        var billId = context.Message.BillId;

        using var transaction = _debtContext.Database.BeginTransaction();

        try
        {
            Bill? bill = await _debtContext
                .Bills
                .FirstAsync(t => t.Id == billId, context.CancellationToken);

            if (bill is null || bill.Status == ProcessingState.Cancelled)
            {
                throw new Exception("Bill already cancelled");
            }

            bill.Status = ProcessingState.Cancelled;

            _debtContext.LedgerRecords
                .Where(q => q.BillId == billId)
                .ExecuteUpdate(l => l.SetProperty(p => p.IsCanceled, true));

            _debtContext.Spendings
                .Where(q => q.BillId == billId)
                .ExecuteUpdate(s => s.SetProperty(p => p.IsCanceled, true));

            await _debtContext.SaveChangesAsync();
            await transaction.CommitAsync();

            await _publishEndpoint.Publish(new BillProcessed() { BillId = billId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling bill");
            await transaction.RollbackAsync();
            await _publishEndpoint.Publish(new BillCancellationFailed(billId));
        }
    }
}
