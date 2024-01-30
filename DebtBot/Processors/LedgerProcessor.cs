using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.DB.Enums;
using DebtBot.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DebtBot.Processors
{
    public class LedgerProcessor : IProcessor
    {
        private DebtContext _debtContext;
        private ProcessorConfiguration _retryConfig;
        private readonly IDbContextFactory<DebtContext> _contextFactory;

        public LedgerProcessor(IDbContextFactory<DebtContext> contextFactory, IOptions<DebtBotConfiguration> debtBotConfig)
        {
            this._contextFactory = contextFactory;
            this._retryConfig = debtBotConfig.Value.LedgerProcessor;
        }

        public int Delay => _retryConfig.ProcessorDelay;

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

            Debt? creditorDebt, debtorDebt;

            using var transaction = debtContext.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);

            for (int retry = 0; retry < _retryConfig.RetryCount; retry++)
            {
                try
                {
                    creditorDebt = debtContext
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

                    debtorDebt = debtContext
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

                    break;
                }
                catch (Exception ex)
                {
                    retry++;
                    if (retry == _retryConfig.RetryCount)
                    {
                        ledgerRecord.Status = ProcessingState.Ready;
                        debtContext.SaveChanges();
                        throw;
                    }
                    await Task.Delay(_retryConfig.RetryDelay);
                }
            }

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
}
