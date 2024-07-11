using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using MassTransit;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DebtBot.Telegram.Commands.CallbackQueries;

public class FinalizeExchangeCommand : ITelegramCallbackQuery
{
    public const string CommandString = "/FinalizeExchange";
    public string CommandName => CommandString;

    private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public FinalizeExchangeCommand(IBillService billService, IPublishEndpoint publishEndpoint)
    {
        _billService = billService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<string?> ExecuteAsync(
        CallbackQuery query,
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var guidStrings = query.Data!.Split(" ")[1];
        if (string.IsNullOrWhiteSpace(guidStrings))
        {
            return "no guids detected";
        }

        var byteArray = Convert.FromBase64String(guidStrings);

        Guid forwardBillId = new Guid(byteArray[..16]);
        Guid backwardBillId = new Guid(byteArray[16..]);

        await FinalizeAsync(forwardBillId, query.Message!.Chat.Id, botClient, cancellationToken);
        await FinalizeAsync(backwardBillId, query.Message!.Chat.Id, botClient, cancellationToken);

        return null;
    }

    private async Task FinalizeAsync(
        Guid billId,
        long chatId,
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var ok = _billService.Finalize(billId);
        if (!ok)
        {
            await _publishEndpoint.Publish(new SendTelegramMessage(chatId, "There was an error finalizing the bill"));
        }
    }

    public static string FormatCallbackData(Guid forwardBillId, Guid backwardBillId)
    {
        byte[] result = new byte[32];
        forwardBillId.ToByteArray().CopyTo(result, 0);
        backwardBillId.ToByteArray().CopyTo(result, 16);

        var base64string = Convert.ToBase64String(result);

        return $"{CommandString} {base64string}";
    }
}
