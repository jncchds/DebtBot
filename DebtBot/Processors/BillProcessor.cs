using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.DB.Enums;
using DebtBot.Messages;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DebtBot.Processors;

public class BillProcessor : IConsumer<BillFinalized>
{
    private ProcessorConfiguration _retryConfig;
    private readonly IDbContextFactory<DebtContext> _contextFactory;
    private readonly IPublishEndpoint _publishEndpoint;

    public int Delay => _retryConfig.ProcessorDelay;

    public BillProcessor(IDbContextFactory<DebtContext> contextFactory, IOptions<DebtBotConfiguration> debtBotConfig, IPublishEndpoint publishEndpoint)
    {
        _contextFactory = contextFactory;
        _publishEndpoint = publishEndpoint;
        _retryConfig = debtBotConfig.Value.BillProcessor;
    }

    public async Task Run(Guid billId, CancellationToken cancellationToken)
    {
        using var debtContext = _contextFactory.CreateDbContext();

        var bill = debtContext
            .Bills
            .Include(t => t.Lines)
            .ThenInclude(t => t.Participants)
            .Include(t => t.Payments)
            .First(t => t.Id == billId);
        
        bill.Status = ProcessingState.Processing;
        debtContext.SaveChanges();

        decimal spentBillTotalNoTips = bill.Lines.Sum(t => t.Subtotal);
        decimal paidPaymentTotalWithTips = bill.Payments.Sum(t => t.Amount);
        Dictionary<Guid, decimal> spentPaymentPerUserWithTips = new Dictionary<Guid, decimal>();

        decimal exchangeRatePaymentToBill = bill.TotalWithTips / paidPaymentTotalWithTips;
        
        foreach (var line in bill.Lines)
        {
            var parts = line.Participants.Sum(t => t.Part);
            foreach (var participant in line.Participants)
            {
                if (!spentPaymentPerUserWithTips.ContainsKey(participant.UserId))
                {
                    spentPaymentPerUserWithTips[participant.UserId] = 0;
                }

                spentPaymentPerUserWithTips[participant.UserId] += line.Subtotal 
                    * participant.Part / parts 
                    * paidPaymentTotalWithTips / spentBillTotalNoTips;
            }
        }
        
        /// spent/pay               - Spent/Paid
        /// currency                - Bill/Payment
        /// total or per            - Total/PerUser
        /// tips/no tips            - WithTips/NoTips
        ///
        /// spentBillTotalNoTips
        /// paidPaymentTotalWithTips
        /// spentBillTotalWithTips
        /// spentPaymentPerUserWithTips

        /// spentBillTotalNoTips - amount spent without tips in bill currency
        /// paidPaymentTotalWithTisp - amount paid with tips in payment currency
        /// bill.total - amount spent with tips in bill currency
        /// spentPaymentPerUserWithTips.value - amount spent with tips in payment currency per user
        
        debtContext.Spendings.AddRange(
            spentPaymentPerUserWithTips.Select(q => new Spending()
            {
                Description = $"{bill.Description} portion {q.Value / paidPaymentTotalWithTips}",
                BillId = bill.Id,
                UserId = q.Key,
                Amount = q.Value * exchangeRatePaymentToBill,
                CurrencyCode = bill.CurrencyCode,
                PaymentCurrencyCode = bill.PaymentCurrencyCode,
                PaymentAmount = q.Value
            }));
        
        foreach (var payment in bill.Payments)
        {
            if (!spentPaymentPerUserWithTips.ContainsKey(payment.UserId))
            {
                spentPaymentPerUserWithTips[payment.UserId] = 0;
            }
            
            spentPaymentPerUserWithTips[payment.UserId] -= payment.Amount;
        }

        var sponsor = spentPaymentPerUserWithTips.MinBy(t => t.Value).Key;
        var records = new List<LedgerRecord>();
        foreach (var item in spentPaymentPerUserWithTips)
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
                CurrencyCode = bill.PaymentCurrencyCode
            });

            var debtor = debtContext.Users.First(t => t.Id == item.Key);
            await _publishEndpoint.Publish(new EnsureContact 
            {
                UserId = sponsor,
                ContactUserId = item.Key, 
                DisplayName = debtor.DisplayName ?? debtor.Id.ToString()
            });

        }

        debtContext.LedgerRecords.AddRange(records);
        bill.Status = ProcessingState.Processed;
        debtContext.SaveChanges();

        await _publishEndpoint.PublishBatch(records.Select(q =>
            new LedgerRecordCreated(q.CreditorUserId, q.DebtorUserId, q.Amount, q.CurrencyCode)
        ));
        
        await _publishEndpoint.PublishBatch(records.Select(q =>
            new LedgerRecordCreated(q.DebtorUserId, q.CreditorUserId, -q.Amount, q.CurrencyCode)
        ));
    }

    public async Task Consume(ConsumeContext<BillFinalized> context)
    {
        await Run(context.Message.id, context.CancellationToken);
    }
}
