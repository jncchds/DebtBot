using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Models.Enums;
using DebtBot.Telegram.Commands;
using MassTransit;
using Microsoft.IdentityModel.Tokens;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Processors;

public class SendBillMessageConsumer : IConsumer<SendBillMessage>
{
    private readonly ITelegramBotClient _botClient;
    private readonly IBillService _billService;

    public SendBillMessageConsumer(ITelegramBotClient botClient, IBillService billService)
    {
        _botClient = botClient;
        _billService = billService;
    }
    public async Task Consume(ConsumeContext<SendBillMessage> context)
    {
        var bill = _billService.Get(context.Message.BillId);

        if (bill is null)
        {
            await context.Publish(new SendTelegramMessage(
                context.Message.ChatId, 
                "Invalid bill id"));
            return;
        }

        var markup = new List<InlineButtonRecord>();
        if (bill!.Status == ProcessingState.Draft)
        {
            markup.Add(new ("Finalize", FinalizeBillCommand.FormatCallbackData(context.Message.BillId)));
        }

        await context.Publish(new SendTelegramMessage(
            context.Message.ChatId, 
            bill!.ToString(), 
            InlineKeyboard: [ markup ]));
    }
}
