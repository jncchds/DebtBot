using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Messages;
using MassTransit;

namespace DebtBot.Consumers;

public class EnsureContactConsumer : IConsumer<EnsureContact>
{
    private DebtContext _debtContext;
    private ILogger<EnsureContactConsumer> _logger;

    public EnsureContactConsumer(DebtContext debtContext, ILogger<EnsureContactConsumer> logger)
    {
        _debtContext = debtContext;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<EnsureContact> context)
    {
        var contact = _debtContext.UserContactsLinks.FirstOrDefault(t => t.UserId == context.Message.UserId && t.ContactUserId == context.Message.ContactUserId);
        if (contact is null)
        {
            contact = new UserContactLink()
            {
                UserId = context.Message.UserId,
                ContactUserId = context.Message.ContactUserId,
                DisplayName = context.Message.DisplayName
            };
            _debtContext.UserContactsLinks.Add(contact);
            _debtContext.SaveChanges();
        }

        return Task.CompletedTask;
    }
}
