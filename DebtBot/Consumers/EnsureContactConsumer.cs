using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using MassTransit;

namespace DebtBot.Consumers;

public class EnsureContactConsumer : IConsumer<EnsureContact>
{
    private readonly IUserContactService _userContactService;

    public EnsureContactConsumer(IUserContactService userContactService)
    {
        _userContactService = userContactService;
    }

    public Task Consume(ConsumeContext<EnsureContact> context)
    {
        _userContactService.EnsureContact(context.Message.UserId, context.Message.ContactUserId, context.Message.DisplayName);

        return Task.CompletedTask;
    }
}
