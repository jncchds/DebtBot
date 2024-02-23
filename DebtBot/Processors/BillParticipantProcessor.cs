using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Messages;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Processors;

public class BillParticipantProcessor : IConsumer<EnsureBillParticipant>
{
    private readonly IDbContextFactory<DebtContext> _contextFactory;

    public BillParticipantProcessor(IDbContextFactory<DebtContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task Consume(ConsumeContext<EnsureBillParticipant> context)
    {
        using var debtContext = _contextFactory.CreateDbContext();

        var participant = await debtContext.BillParticipants.FirstOrDefaultAsync(t =>
            t.BillId == context.Message.BillId && t.UserId == context.Message.UserId);
        if (participant == null)
        {
            await debtContext.BillParticipants.AddAsync(new BillParticipant
            {
                BillId = context.Message.BillId,
                UserId = context.Message.UserId
            });

            await debtContext.SaveChangesAsync();
        }
    }
}
