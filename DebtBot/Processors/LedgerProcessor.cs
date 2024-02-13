using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Messages;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;

namespace DebtBot.Processors;

public class LedgerProcessor : IConsumer<LedgerRecordCreated>
{
    private ProcessorConfiguration _retryConfig;
    private readonly IDbContextFactory<DebtContext> _contextFactory;
    private readonly ISyncPolicy _retryPolicy;
    private readonly ILogger<LedgerProcessor> _logger;

    public int Delay => _retryConfig.ProcessorDelay;

    public LedgerProcessor(IDbContextFactory<DebtContext> contextFactory, IOptions<DebtBotConfiguration> debtBotConfig, ILogger<LedgerProcessor> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _retryConfig = debtBotConfig.Value.LedgerProcessor;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(_retryConfig.RetryCount, _ => TimeSpan.FromMilliseconds(_retryConfig.RetryDelay));
    }

    public async Task Consume(ConsumeContext<LedgerRecordCreated> context)
    {
        using var debtContext = _contextFactory.CreateDbContext();

        try
        {
            _retryPolicy.Execute(() => ApplyRecordToDebt(debtContext, context.Message));
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "retry failed");
            throw;
        }
    }

    private void ApplyRecordToDebt(DebtContext debtContext, LedgerRecordCreated message)
    {
        using var transaction = debtContext.Database.BeginTransaction();

        var creditor = debtContext
            .Users
            .First(t => t.Id == message.CreditorUserId);

        var creditorDebt = creditor.Debts
            .FirstOrDefault(t => t.DebtorUserId == message.DebtorUserId
                                && t.CurrencyCode == message.CurrencyCode);

        if (creditorDebt == null)
        {
            creditorDebt = new Debt()
            {
                Amount = 0.0m,
                CurrencyCode = message.CurrencyCode,
                DebtorUserId = message.DebtorUserId,
                CreditorUserId = message.CreditorUserId
            };

            creditor.Debts.Add(creditorDebt);
        }

        creditorDebt.Amount += message.Amount;
        creditor.Version++;

        debtContext.SaveChanges();
        transaction.Commit();
    }
}
