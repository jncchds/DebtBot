using System.Collections.Concurrent;
using System.Reflection;

namespace DebtBot.Receipts;

/// <summary>
/// An <see cref="ILlmClient"/> without a model, for tests and for running the app without a key
/// (<c>Llm:Provider=Fake</c>, Development only). Answers come from, in order:
/// <list type="number">
/// <item>answers a test queued with <c>Enqueue</c>;</item>
/// <item>the fixture whose name the request's hint (the uploaded file's name) contains, for example
/// <c>supermarket-discount.jpg</c>; the hints <c>busy</c>, <c>timeout</c> and <c>upstream</c> give those errors;</item>
/// <item>the <c>cafe-eur</c> fixture.</item>
/// </list>
/// The fixtures are <c>DebtBot.Tests/Fixtures/receipts/*.json</c>, embedded in this assembly.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    public const string ModelName = "fake/receipt-fixtures";
    public const string DefaultFixture = "cafe-eur";
    private const string ResourcePrefix = "DebtBot.ReceiptFixtures.";

    private readonly ConcurrentQueue<Func<LlmRequest, LlmResult>> _queued = new();
    private readonly ConcurrentQueue<LlmRequest> _requests = new();

    /// <summary>Every request, in order (tests check the count for the retry).</summary>
    public IReadOnlyCollection<LlmRequest> Requests => _requests;

    public void Enqueue(LlmResult result) => _queued.Enqueue(_ => result);

    public void Enqueue(Func<LlmRequest, LlmResult> answer) => _queued.Enqueue(answer);

    /// <summary>Queues a fixture's content as the model's answer (invalid JSON gives <see cref="LlmOutcome.InvalidJson"/>).</summary>
    public void EnqueueFixture(string name) => _queued.Enqueue(_ => FromContent(Fixture(name)));

    public Task<LlmResult> CompleteJsonAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request);
        if (_queued.TryDequeue(out var answer))
        {
            return Task.FromResult(answer(request));
        }

        var hint = (request.Hint ?? "").ToLowerInvariant();
        if (hint.Contains("busy")) return Task.FromResult(LlmResult.Error(LlmOutcome.Busy, 429, ModelName));
        if (hint.Contains("timeout")) return Task.FromResult(LlmResult.Error(LlmOutcome.Timeout, null, ModelName));
        if (hint.Contains("upstream")) return Task.FromResult(LlmResult.Error(LlmOutcome.Upstream, 502, ModelName));

        var name = FixtureNames().OrderByDescending(n => n.Length).FirstOrDefault(n => hint.Contains(n)) ?? DefaultFixture;
        return Task.FromResult(FromContent(Fixture(name)));
    }

    public static LlmResult FromContent(string content)
    {
        var json = LlmJson.ParseObject(content);
        return new LlmResult(json is null ? LlmOutcome.InvalidJson : LlmOutcome.Ok, ModelName, json, 0, 0, 200);
    }

    /// <summary>The fixture names (file names without <c>.json</c>).</summary>
    public static IEnumerable<string> FixtureNames() =>
        Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => n[ResourcePrefix.Length..^".json".Length]);

    public static string Fixture(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + name + ".json")
            ?? throw new ArgumentException($"No receipt fixture '{name}'.", nameof(name));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
