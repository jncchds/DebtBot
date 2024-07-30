using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models.Enums;
using DebtBot.Messages;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using DebtBot.Messages.Notification;

namespace DebtBot.Consumers;

public class BillFinalizedConsumer : IConsumer<BillFinalized>
{
    private readonly DebtContext _debtContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<BillFinalizedConsumer> _logger;

    public BillFinalizedConsumer(DebtContext debtContext, IPublishEndpoint publishEndpoint, ILogger<BillFinalizedConsumer> logger)
    {
        _debtContext = debtContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }


    public async Task Consume(ConsumeContext<BillFinalized> context)
    {
        var billId = context.Message.id;

        Bill? bill = null;

        using var transaction = _debtContext.Database.BeginTransaction();

        try
        {

            bill = await _debtContext
                .Bills
                .Include(t => t.Lines)
                .ThenInclude(t => t.Participants)
                .Include(t => t.Payments)
                .FirstAsync(t => t.Id == billId, context.CancellationToken);

            bill.Status = ProcessingState.Processing;
            await _debtContext.SaveChangesAsync();

            decimal spentBillTotalNoTips = bill.Lines.Sum(t => t.Subtotal);
            decimal paidPaymentTotalWithTips = bill.Payments.Sum(t => t.Amount);
            Dictionary<Guid, decimal> spentPaymentPerUserWithTips = new Dictionary<Guid, decimal>();

            decimal exchangeRatePaymentToBill = bill.TotalWithTips / paidPaymentTotalWithTips;

            foreach (var line in bill.Lines)
            {
                var parts = line.Participants.Sum(t => t.Part);
                foreach (var participant in line.Participants)
                {
                    spentPaymentPerUserWithTips.TryAdd(participant.UserId, 0);

                    spentPaymentPerUserWithTips[participant.UserId] += line.Subtotal
                        * participant.Part / parts
                        * paidPaymentTotalWithTips / spentBillTotalNoTips;
                }
            }

            // spent/pay               - Spent/Paid
            // currency                - Bill/Payment
            // total or per            - Total/PerUser
            // tips/no tips            - WithTips/NoTips
            //
            // spentBillTotalNoTips
            // paidPaymentTotalWithTips
            // spentBillTotalWithTips
            // spentPaymentPerUserWithTips

            // spentBillTotalNoTips - amount spent without tips in bill currency
            // paidPaymentTotalWithTips - amount paid with tips in payment currency
            // bill.total - amount spent with tips in bill currency
            // spentPaymentPerUserWithTips.value - amount spent with tips in payment currency per user

            _debtContext.Spendings.AddRange(
                spentPaymentPerUserWithTips.Select(q => new Spending()
                {
                    Description = bill.Description,
                    Portion = q.Value / paidPaymentTotalWithTips,
                    BillId = bill.Id,
                    UserId = q.Key,
                    Amount = q.Value * exchangeRatePaymentToBill,
                    CurrencyCode = bill.CurrencyCode,
                    PaymentCurrencyCode = bill.PaymentCurrencyCode,
                    PaymentAmount = q.Value,
                    Date = bill.Date
                }));

            foreach (var payment in bill.Payments)
            {
                spentPaymentPerUserWithTips.TryAdd(payment.UserId, 0);
                spentPaymentPerUserWithTips[payment.UserId] -= payment.Amount;
            }

            var sponsorId = context.Message.forceSponsor ?
                bill.CreatorId :
                spentPaymentPerUserWithTips.MinBy(t => t.Value).Key;
            var sponsor = _debtContext.Users.First(t => t.Id == sponsorId);
            var records = new List<LedgerRecord>();
            foreach (var item in spentPaymentPerUserWithTips)
            {
                if (item.Key == sponsorId)
                {
                    continue;
                }

                records.Add(new LedgerRecord()
                {
                    Amount = item.Value,
                    CreditorUserId = sponsorId,
                    DebtorUserId = item.Key,
                    BillId = bill.Id,
                    CurrencyCode = bill.PaymentCurrencyCode
                });

                var debtor = _debtContext.Users.First(t => t.Id == item.Key);
                await _publishEndpoint.Publish(new EnsureContact(
                    sponsorId,
                    item.Key,
                    debtor.DisplayName));
                await _publishEndpoint.Publish(new EnsureContact(
                    item.Key,
                    sponsorId,
                    sponsor.DisplayName ?? sponsorId.ToString()));
            }

            _debtContext.LedgerRecords.AddRange(records);
            bill.Status = ProcessingState.Processed;
            await _debtContext.SaveChangesAsync();

            await transaction.CommitAsync();

            await _publishEndpoint.Publish(new BillProcessed() { BillId = bill.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bill");
            await transaction.RollbackAsync();
            if (bill != null)
            {
                bill.Status = ProcessingState.Draft;
                await _debtContext.SaveChangesAsync();
            }
            await _publishEndpoint.Publish(new BillProcessFailed() { BillId = bill.Id });
        }
    }
}
