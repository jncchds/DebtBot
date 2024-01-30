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
        private Retries _retryConfig;

        public LedgerProcessor(DebtContext debtContext, IOptions<DebtBotConfiguration> debtBotConfig)
        {
            this._debtContext = debtContext;
            this._retryConfig = debtBotConfig.Value.LedgerProcessor;
        }

        public int Delay => 2000;

        public async Task Run(CancellationToken token)
        {
            var ledgerRecordIds = GetUnprocessedLedgerRecordId();
            if (ledgerRecordIds is null)
            {
                return;
            }

            var (creditorId, debtorId, billId) = ledgerRecordIds.Value;

            var ledgerRecord = await _debtContext
                .LedgerRecords
                .Include(t => t.CreditorUser)
                .Include(t => t.DebtorUser)
                .Include(t => t.Bill)
                .FirstAsync(t => t.CreditorUserId == creditorId
                                   && t.DebtorUserId == debtorId
                                   && t.BillId == billId,
                                   token);

            Debt? creditorDebt, debtorDebt;

            using var transaction = await _debtContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token);

            for (int retry = 0; retry < _retryConfig.RetryCount; retry++)
            {
                try
                {
                    creditorDebt = await _debtContext
                        .Debts
                        .FirstOrDefaultAsync(t => t.CreditorUserId == creditorId 
                            && t.DebtorUserId == debtorId 
                            && t.CurrencyCode == ledgerRecord.CurrencyCode, 
                            token);

                    if (creditorDebt == null)
                    {
                        creditorDebt = new Debt
                        {
                            CreditorUserId = creditorId,
                            DebtorUserId = debtorId,
                            Amount = 0,
                            CurrencyCode = ledgerRecord.CurrencyCode
                        };

                        _debtContext.Debts.Add(creditorDebt);
                    }

                    debtorDebt = await _debtContext
                        .Debts
                        .FirstOrDefaultAsync(t => t.CreditorUserId == debtorId 
                            && t.DebtorUserId == creditorId 
                            && t.CurrencyCode == ledgerRecord.CurrencyCode,
                            token);

                    if (debtorDebt == null)
                    {
                        debtorDebt = new Debt
                        {
                            CreditorUserId = debtorId,
                            DebtorUserId = creditorId,
                            Amount = 0,
                            CurrencyCode = ledgerRecord.CurrencyCode
                        };

                        _debtContext.Debts.Add(debtorDebt);
                    }

                    creditorDebt.Amount += ledgerRecord.Amount;
                    debtorDebt.Amount -= ledgerRecord.Amount;
                    ledgerRecord.Status = ProcessingState.Processed;

                    await _debtContext.SaveChangesAsync(token);

                    break;
                }
                catch (Exception ex)
                {
                    retry++;
                    if (retry == _retryConfig.RetryCount)
                    {
                        ledgerRecord.Status = ProcessingState.Ready;
                        await _debtContext.SaveChangesAsync(token);
                        throw;
                    }
                    await Task.Delay(_retryConfig.RetryDelay);
                }
            }

            await transaction.CommitAsync(token);
        }

        private (Guid creditorId, Guid debtorId, Guid billId)? GetUnprocessedLedgerRecordId()
        {
            using (var transaction = _debtContext.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {

                var ledgerRecord = _debtContext
                    .LedgerRecords
                    .FirstOrDefault(t => t.Status == ProcessingState.Ready);

                if (ledgerRecord is null)
                    return null;

                ledgerRecord.Status = ProcessingState.Processing;

                _debtContext.SaveChanges();

                transaction.Commit();

                return (ledgerRecord.CreditorUserId, ledgerRecord.DebtorUserId, ledgerRecord.BillId);
            }
        }
    }
}
