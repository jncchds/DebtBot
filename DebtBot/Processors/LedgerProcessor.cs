using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.DB.Enums;
using DebtBot.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;

namespace DebtBot.Processors;

public class LedgerProcessor : IProcessor
{
    private ProcessorConfiguration _retryConfig;
    private readonly IDbContextFactory<DebtContext> _contextFactory;

    public LedgerProcessor(IDbContextFactory<DebtContext> contextFactory, IOptions<DebtBotConfiguration> debtBotConfig)
    {
        this._contextFactory = contextFactory;
        this._retryConfig = debtBotConfig.Value.LedgerProcessor;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(_retryConfig.RetryCount, _ => TimeSpan.FromSeconds(_retryConfig.RetryDelay));
    }

    public int Delay => _retryConfig.ProcessorDelay;

    private readonly ISyncPolicy _retryPolicy;

    public async Task Run(CancellationToken token)
    {
        using var debtContext = _contextFactory.CreateDbContext();
        var ledgerRecordIds = GetUnprocessedLedgerRecordId(debtContext);
        if (ledgerRecordIds is null)
        {
            return;
        }

        var (creditorId, debtorId, billId) = ledgerRecordIds.Value;

        var ledgerRecord = debtContext
            .LedgerRecords
            .Include(t => t.CreditorUser)
            .Include(t => t.DebtorUser)
            .Include(t => t.Bill)
            .First(t => t.CreditorUserId == creditorId
                               && t.DebtorUserId == debtorId
                               && t.BillId == billId);

        try
        {
            using var transaction = debtContext.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            _retryPolicy.Execute(() => ApplyRecordsToDebt(creditorId, debtorId, ledgerRecord));
            
            transaction.Commit();
        }
        catch (Exception)
        {
            ledgerRecord.Status = ProcessingState.Ready;
            debtContext.SaveChanges();
        }
    }

    private void ApplyRecordsToDebt(Guid creditorId, Guid debtorId, LedgerRecord ledgerRecord)
    {
        using var debtContext = _contextFactory.CreateDbContext();
        var creditorDebt = debtContext
            .Debts
            .FirstOrDefault(t => t.CreditorUserId == creditorId
                                 && t.DebtorUserId == debtorId
                                 && t.CurrencyCode == ledgerRecord.CurrencyCode);

        if (creditorDebt == null)
        {
            creditorDebt = new Debt
            {
                CreditorUserId = creditorId,
                DebtorUserId = debtorId,
                Amount = 0,
                CurrencyCode = ledgerRecord.CurrencyCode
            };

            debtContext.Debts.Add(creditorDebt);
        }

        var debtorDebt = debtContext
            .Debts
            .FirstOrDefault(t => t.CreditorUserId == debtorId
                                 && t.DebtorUserId == creditorId
                                 && t.CurrencyCode == ledgerRecord.CurrencyCode);

        if (debtorDebt == null)
        {
            debtorDebt = new Debt
            {
                CreditorUserId = debtorId,
                DebtorUserId = creditorId,
                Amount = 0,
                CurrencyCode = ledgerRecord.CurrencyCode
            };

            debtContext.Debts.Add(debtorDebt);
        }

        creditorDebt.Amount += ledgerRecord.Amount;
        debtorDebt.Amount -= ledgerRecord.Amount;
        ledgerRecord.Status = ProcessingState.Processed;

        debtContext.SaveChanges();
    }

    private (Guid creditorId, Guid debtorId, Guid billId)? GetUnprocessedLedgerRecordId(DebtContext debtContext)
    {
        using (var transaction = debtContext.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
        {

            var ledgerRecord = debtContext
                .LedgerRecords
                .FirstOrDefault(t => t.Status == ProcessingState.Ready);

            if (ledgerRecord is null)
                return null;

            ledgerRecord.Status = ProcessingState.Processing;

            debtContext.SaveChanges();

            transaction.Commit();

            return (ledgerRecord.CreditorUserId, ledgerRecord.DebtorUserId, ledgerRecord.BillId);
        }
    }
}
