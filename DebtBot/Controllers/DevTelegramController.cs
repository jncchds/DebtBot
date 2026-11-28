using DebtBot.Identity;
using DebtBot.Messages;
using DebtBot.Telegram;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace DebtBot.Controllers;

/// <summary>
/// <b>Development only</b> (404 elsewhere), admins only: runs a Bot API <see cref="Update"/> through the bot's
/// <see cref="UpdateHandler"/> without Telegram, to check the bot on a local stack (receipts T2 and T4). Any
/// <c>getFile</c> is served the uploaded <c>file</c>, in memory. Messages the handler itself and the receipt photo
/// handler send are returned instead of sent; commands still publish theirs to the bus as usual.
/// </summary>
[Authorize(IdentityData.AdminUserPolicyName)]
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/v1/dev/telegram")]
public class DevTelegramController : DebtBotControllerBase
{
    public const long DevBotId = 1;

    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _environment;

    public DevTelegramController(IServiceProvider services, IHostEnvironment environment)
    {
        _services = services;
        _environment = environment;
    }

    /// <summary>Multipart: <c>update</c> (the update's JSON, as Telegram sends it) and an optional <c>file</c>.</summary>
    [HttpPost("update")]
    [RequestSizeLimit(BillReceiptController.MaxRequestBytes * 2)]
    public async Task<IActionResult> Update([FromForm] string update, IFormFile? file, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var parsed = JsonSerializer.Deserialize<Update>(update, JsonBotAPI.Options);
        if (parsed is null)
        {
            return BadRequest(new { message = "No update." });
        }

        byte[] bytes = [];
        if (file is not null)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            bytes = stream.ToArray();
        }

        var bot = new DevBotClient(bytes);
        var sent = new CapturingPublishEndpoint(_services.GetRequiredService<IPublishEndpoint>());
        var photos = ActivatorUtilities.CreateInstance<ReceiptPhotoHandler>(_services, (IPublishEndpoint)sent);
        var handler = ActivatorUtilities.CreateInstance<UpdateHandler>(_services, (ITelegramBotClient)bot, (IPublishEndpoint)sent, photos);

        await handler.HandleUpdateAsync(bot, parsed, cancellationToken);

        return Ok(new { replies = sent.Messages, downloads = bot.Downloads });
    }

    /// <summary>Keeps the Telegram messages, passes everything else on to the bus.</summary>
    private sealed class CapturingPublishEndpoint(IPublishEndpoint inner) : IPublishEndpoint
    {
        public List<TelegramMessageRequested> Messages { get; } = [];

        private Task Capture<T>(T message, Func<Task> otherwise)
        {
            if (message is TelegramMessageRequested telegram)
            {
                Messages.Add(telegram);
                return Task.CompletedTask;
            }
            return otherwise();
        }

        public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => inner.ConnectPublishObserver(observer);
        public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class =>
            Capture(message, () => inner.Publish(message, cancellationToken));
        public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class =>
            Capture(message, () => inner.Publish(message, publishPipe, cancellationToken));
        public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class =>
            Capture(message, () => inner.Publish(message, publishPipe, cancellationToken));
        public Task Publish(object message, CancellationToken cancellationToken = default) =>
            Capture(message, () => inner.Publish(message, cancellationToken));
        public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
            Capture(message, () => inner.Publish(message, publishPipe, cancellationToken));
        public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default) =>
            Capture(message, () => inner.Publish(message, messageType, cancellationToken));
        public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
            Capture(message, () => inner.Publish(message, messageType, publishPipe, cancellationToken));
        public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class =>
            inner.Publish<T>(values, cancellationToken);
        public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class =>
            inner.Publish(values, publishPipe, cancellationToken);
        public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class =>
            inner.Publish<T>(values, publishPipe, cancellationToken);
    }

    /// <summary>A bot client that only knows <c>getFile</c> (served from memory) and answers callbacks with true.</summary>
    private sealed class DevBotClient(byte[] file) : ITelegramBotClient
    {
        public List<string> Downloads { get; } = [];

        public bool LocalBotServer => false;
        public long BotId => DevBotId;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        public IExceptionParser ExceptionsParser { get; set; } = new DefaultExceptionParser();
        public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest { add { } remove { } }
        public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived { add { } remove { } }

        public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object? response = request switch
            {
                GetFileRequest get => new TGFile { FileId = get.FileId, FileUniqueId = get.FileId, FilePath = $"dev/{get.FileId}", FileSize = file.Length },
                AnswerCallbackQueryRequest => true,
                _ => throw new NotSupportedException($"The dev bot can't {request.MethodName}."),
            };
            return Task.FromResult((TResponse)response);
        }

        public Task<bool> TestApi(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public async Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default)
        {
            Downloads.Add(filePath);
            await destination.WriteAsync(file, cancellationToken);
        }

        public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default) =>
            DownloadFile(file.FilePath!, destination, cancellationToken);
    }
}
