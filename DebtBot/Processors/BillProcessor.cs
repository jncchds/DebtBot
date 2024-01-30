﻿using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.DB.Enums;
using DebtBot.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DebtBot.Processors
{
    public class BillProcessor : IProcessor
    {
        private DebtContext _debtContext;
        private ProcessorConfiguration _retryConfig;
        private readonly IDbContextFactory<DebtContext> _contextFactory;

        public BillProcessor(IDbContextFactory<DebtContext> contextFactory, IOptions<DebtBotConfiguration> debtBotConfig)
        {
            _contextFactory = contextFactory;
            _retryConfig = debtBotConfig.Value.BillProcessor;
        }

        public int Delay => _retryConfig.ProcessorDelay;

        public async Task Run(CancellationToken token)
        {
            using var debtContext = _contextFactory.CreateDbContext();

            var billId = GetUnprocessedBill(debtContext);
            if (billId is null)
            {
                return;
            }

            var bill = debtContext
                .Bills
                .Include(t => t.Lines)
                .ThenInclude(t => t.Participants)
                .Include(t => t.Payments)
                .First(t => t.Id == billId);

            decimal amount = bill.Lines.Sum(t => t.Subtotal);
            decimal paid = bill.Payments.Sum(t => t.Amount);
            Dictionary<Guid, decimal> participation = new Dictionary<Guid, decimal>();
            foreach (var line in bill.Lines)
            {
                var parts = line.Participants.Sum(t => t.Part);
                foreach (var participant in line.Participants)
                {
                    if (!participation.ContainsKey(participant.UserId))
                    {
                        participation[participant.UserId] = 0;
                    }

                    participation[participant.UserId] += participant.Part * line.Subtotal / parts;
                }
            }

            foreach (var item in participation)
            {
                participation[item.Key] = item.Value * paid / amount;
            }

            foreach (var payment in bill.Payments)
            {
                if (!participation.ContainsKey(payment.UserId))
                {
                    participation[payment.UserId] = 0;
                }

                participation[payment.UserId] -= payment.Amount;
            }

            var sponsor = participation.MinBy(t => t.Value).Key;
            var records = new List<LedgerRecord>();
            foreach (var item in participation)
            {
                if (item.Key == sponsor)
                {
                    continue;
                }

                records.Add(new LedgerRecord()
                {
                    Amount = item.Value,
                    CreditorUserId = sponsor,
                    DebtorUserId = item.Key,
                    BillId = bill.Id,
                    CurrencyCode = bill.CurrencyCode,
                    Status = ProcessingState.Ready
                });

                try
                {
                    var contact = debtContext.UserContactsLinks.FirstOrDefault(t => t.UserId == sponsor && t.ContactUserId == item.Key);
                    if (contact is null)
                    {
                        var debtor = debtContext.Users.First(t => t.Id == item.Key);
                        contact = new UserContactLink()
                        {
                            UserId = sponsor,
                            ContactUserId = item.Key,
                            DisplayName = debtor.DisplayName ?? debtor.Id.ToString()
                        };
                        debtContext.UserContactsLinks.Add(contact);
                        debtContext.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    // TODO: Log
                }
            }

            debtContext.LedgerRecords.AddRange(records);
            bill.Status = ProcessingState.Processed;
            debtContext.SaveChanges();
        }

        private Guid? GetUnprocessedBill(DebtContext debtContext)
        {
            using var transaction = debtContext.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);

            var bill = debtContext
                .Bills
                .FirstOrDefault(t => t.Status == ProcessingState.Ready);

            if (bill is null)
                return null;

            bill.Status = ProcessingState.Processing;

            debtContext.SaveChanges();

            transaction.Commit();

            return bill.Id;
        }
    }
}