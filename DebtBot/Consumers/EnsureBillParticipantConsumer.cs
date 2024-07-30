using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Messages;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Consumers;

public class EnsureBillParticipantConsumer : IConsumer<EnsureBillParticipant>
{
    private readonly DebtContext _debtContext;
    private readonly ILogger<EnsureBillParticipantConsumer> _logger;

    public EnsureBillParticipantConsumer(
        DebtContext debtContext, 
        ILogger<EnsureBillParticipantConsumer> logger)
    {
        _debtContext = debtContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EnsureBillParticipant> context)
    {
        var participant = await _debtContext.BillParticipants.FirstOrDefaultAsync(t =>
            t.BillId == context.Message.BillId && t.UserId == context.Message.UserId);
        
        if (participant == null)
        {
            await _debtContext.BillParticipants.AddAsync(new BillParticipant
            {
                BillId = context.Message.BillId,
                UserId = context.Message.UserId
            });

            await _debtContext.SaveChangesAsync();
        }
    }
}
