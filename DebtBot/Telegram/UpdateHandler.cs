using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram;

public class UpdateHandler : IUpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly IEnumerable<ITelegramCommand> _commands;
    private readonly IEnumerable<ITelegramCallbackQuery> _callbackQueries;
    private readonly ITelegramService _telegramService;
    private readonly MassTransit.IPublishEndpoint _publishEndpoint;

    public UpdateHandler(
        ITelegramBotClient botClient,
        ILogger<UpdateHandler> logger,
        IEnumerable<ITelegramCommand> commands,
        IEnumerable<ITelegramCallbackQuery> callbackQueries, 
        ITelegramService telegramService,
        MassTransit.IPublishEndpoint publishEndpoint)
    {
        _botClient = botClient;
        _logger = logger;
        _commands = commands;
        _callbackQueries = callbackQueries;
        _telegramService = telegramService;
        _publishEndpoint = publishEndpoint;
    }
    public async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogInformation("HandleError: {ErrorMessage}", ErrorMessage);

        // Cooldown in case of network connection error
        if (exception is RequestException)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            var handler = update switch
            {
                // UpdateType.Unknown:
                // UpdateType.ChannelPost:
                // UpdateType.EditedChannelPost:
                // UpdateType.ShippingQuery:
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
                { Message: { } message } => BotOnMessageReceived(message, cancellationToken),
                { EditedMessage: { } message } => BotOnMessageReceived(message, cancellationToken),
                { CallbackQuery: { } callbackQuery } => BotOnCallbackQueryReceived(callbackQuery, cancellationToken),
                { InlineQuery: { } inlineQuery } => BotOnInlineQueryReceived(inlineQuery, cancellationToken),
                { ChosenInlineResult: { } chosenInlineResult } => BotOnChosenInlineResultReceived(chosenInlineResult, cancellationToken),
                _ => UnknownUpdateHandlerAsync(update, cancellationToken)
            };

            await handler;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occured while handling update");
        }

    }

    private async Task UnknownUpdateHandlerAsync(Update update, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task BotOnChosenInlineResultReceived(ChosenInlineResult chosenInlineResult, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task BotOnInlineQueryReceived(InlineQuery inlineQuery, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var callback = _callbackQueries.FirstOrDefault(t =>
                   (callbackQuery.Data?.StartsWith($"{t.CommandName} ", StringComparison.InvariantCultureIgnoreCase) ?? false)
                   || 
                   (callbackQuery.Data?.Equals(t.CommandName, StringComparison.InvariantCultureIgnoreCase) ?? false));
        
        if (callback != null)
        {
            string? ret = await callback.ExecuteAsync(callbackQuery, _botClient, cancellationToken);
            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, ret, cancellationToken: cancellationToken);
        }
        else
        {
            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "command not found", cancellationToken: cancellationToken);
        }
    }

    private async Task BotOnMessageReceived(Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type == ChatType.Channel)
        {
            await _publishEndpoint.Publish(new SendTelegramMessage(message.Chat.Id, $"Hello! Currently channels are not supported"));
            return;
        }

        _telegramService.Actualize(message.From!);
        
        var preprocessedMessage = _telegramService.ProcessMessage(message, _botClient.BotId);
        if (preprocessedMessage is not null && !string.IsNullOrEmpty(preprocessedMessage.BotCommand))
        {
            var command = _commands.FirstOrDefault(t => 
                preprocessedMessage.BotCommand.Equals(t.CommandName, StringComparison.InvariantCultureIgnoreCase));
            if (command != null)
            {
                await command.ExecuteAsync(preprocessedMessage, _botClient, cancellationToken);
            }
        }
    }
}