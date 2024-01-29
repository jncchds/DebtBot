using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.DB.Enums;
using DebtBot.Interfaces;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Processors
{
    public class BillProcessor : IProcessor
    {
        private DebtContext _debtContext;

        public BillProcessor(DebtContext debtContext)
        {
            this._debtContext = debtContext;
        }

        public int Delay => 2000;

        public async Task Run(CancellationToken token)
        {
            var billId = GetUnprocessedBill();
            if (billId is null)
            {
                return;
            }

            var bill = _debtContext
                .Bills
                .Include(t => t.Lines)
                .ThenInclude(t => t.Participants)
                .Include(t => t.Payments)
                .FirstOrDefault(t => t.Id == billId);

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
                    var contact = _debtContext.UserContactsLinks.FirstOrDefault(t => t.UserId == sponsor && t.ContactUserId == item.Key);
                    if (contact is null)
                    {
                        var debtor = _debtContext.Users.First(t => t.Id == item.Key);
                        contact = new UserContactLink()
                        {
                            UserId = sponsor,
                            ContactUserId = item.Key,
                            DisplayName = debtor.DisplayName ?? debtor.Id.ToString()
                        };
                        _debtContext.UserContactsLinks.Add(contact);
                        _debtContext.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    // TODO: Log
                }
            }

            _debtContext.LedgerRecords.AddRange(records);
            bill.Status = ProcessingState.Processed;
            _debtContext.SaveChanges();
        }

        private Guid? GetUnprocessedBill()
        {
            using var transaction = _debtContext.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);

            var bill = _debtContext
                .Bills
                .FirstOrDefault(t => t.Status == ProcessingState.Ready);

            if (bill is null)
                return null;

            bill.Status = ProcessingState.Processing;

            _debtContext.SaveChanges();

            transaction.Commit();

            return bill.Id;
        }
    }
}
