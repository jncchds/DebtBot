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
    private readonly ISyncPolicy _retryPolicy;

    public int Delay => _retryConfig.ProcessorDelay;

    public LedgerProcessor(IDbContextFactory<DebtContext> contextFactory, IOptions<DebtBotConfiguration> debtBotConfig)
    {
        this._contextFactory = contextFactory;
        this._retryConfig = debtBotConfig.Value.LedgerProcessor;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(_retryConfig.RetryCount, _ => TimeSpan.FromMilliseconds(_retryConfig.RetryDelay));
    }

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
            _retryPolicy.Execute(() => ApplyRecordsToDebt(debtContext, creditorId, debtorId, ledgerRecord));
        }
        catch (Exception)
        {
            ledgerRecord.Status = ProcessingState.Ready;
            debtContext.SaveChanges();
        }
    }

    private void ApplyRecordsToDebt(DebtContext debtContext, Guid creditorId, Guid debtorId, LedgerRecord ledgerRecord)
    {
        using var transaction = debtContext.Database.BeginTransaction();

        var creditor = debtContext
            .Users
            .First(t => t.Id == creditorId);

        var debtor = debtContext
            .Users
            .First(t => t.Id == debtorId);

        var creditorDebt = creditor.Debts
            .FirstOrDefault(t => t.DebtorUserId == debtorId
                                && t.CurrencyCode == ledgerRecord.CurrencyCode);

        if (creditorDebt == null)
        {
            creditorDebt = new Debt()
            {
                Amount = 0.0m,
                CurrencyCode = ledgerRecord.CurrencyCode,
                DebtorUserId = debtorId,
                CreditorUserId = creditorId
            };

            creditor.Debts.Add(creditorDebt);
        }

        var debtorDebt = debtor.Debts
            .FirstOrDefault(t => t.DebtorUserId == creditorId
                                && t.CurrencyCode == ledgerRecord.CurrencyCode);

        if (debtorDebt == null)
        {
            debtorDebt = new Debt()
            {
                Amount = 0.0m,
                CurrencyCode = ledgerRecord.CurrencyCode,
                DebtorUserId = creditorId,
                CreditorUserId = debtorId
            };

            debtor.Debts.Add(debtorDebt);
        }

        creditorDebt.Amount += ledgerRecord.Amount;
        debtorDebt.Amount -= ledgerRecord.Amount;
        ledgerRecord.Status = ProcessingState.Processed;
        creditor.Version++;
        debtor.Version++;

        debtContext.SaveChanges();
        transaction.Commit();
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
