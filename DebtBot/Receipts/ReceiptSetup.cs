using Microsoft.Extensions.Options;

namespace DebtBot.Receipts;

/// <summary>Whether receipts can be read here, and with which model (for <c>GET auth/config</c>).</summary>
public sealed class ReceiptFeature
{
    public ReceiptFeature(IOptions<DebtBotConfiguration> options, IHostEnvironment environment)
    {
        var settings = options.Value.Llm;
        IsFake = settings.IsFake;
        // The fake answers with fixtures whatever the photo, so it never runs outside Development.
        IsEnabled = IsFake ? environment.IsDevelopment() : !string.IsNullOrWhiteSpace(settings.ApiKey);
        Model = IsFake ? FakeLlmClient.ModelName : settings.Model;
    }

    public bool IsEnabled { get; }
    public bool IsFake { get; }
    /// <summary>The primary model, for the privacy note ("model X").</summary>
    public string Model { get; }
}

public static class ReceiptSetup
{
    /// <summary>
    /// The receipt services: the LLM client (OpenRouter, or the fake with <c>Llm:Provider=Fake</c>), the per-user
    /// limiter and draft store (singletons), and the scoped <see cref="IReceiptService"/>. Needs
    /// <c>IOptions&lt;DebtBotConfiguration&gt;</c>, a <see cref="TimeProvider"/> and the memory cache.
    /// </summary>
    public static IServiceCollection AddDebtBotReceipts(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection($"{DebtBotConfiguration.SectionName}:Llm").Get<LlmConfiguration>() ?? new LlmConfiguration();

        services.AddMemoryCache();
        services.AddSingleton<ReceiptFeature>();
        services.AddSingleton<IReceiptRateLimiter, ReceiptRateLimiter>();
        services.AddSingleton<IReceiptDraftStore, ReceiptDraftStore>();
        services.AddScoped<IReceiptService, ReceiptService>();

        if (settings.IsFake)
        {
            services.AddSingleton<FakeLlmClient>();
            services.AddSingleton<ILlmClient>(sp => sp.GetRequiredService<FakeLlmClient>());
        }
        else
        {
            services.AddHttpClient<ILlmClient, OpenRouterClient>(client =>
            {
                // Our own timeout (Llm:TimeoutSeconds) is in OpenRouterClient; this one is only a backstop.
                client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds) + 15);
            });
        }

        return services;
    }

    /// <summary>Logs how receipts are set up, once at start-up.</summary>
    public static void LogReceiptStatus(this WebApplication app)
    {
        var feature = app.Services.GetRequiredService<ReceiptFeature>();
        if (feature.IsFake)
        {
            if (feature.IsEnabled)
                app.Logger.LogWarning("Receipts use the fake LLM provider (fixtures, not the photo). Development only.");
            else
                app.Logger.LogWarning("DebtBot:Llm:Provider is Fake, which only runs in Development: receipt reading is off.");
        }
        else if (!feature.IsEnabled)
        {
            app.Logger.LogInformation("DebtBot:Llm:ApiKey is not set: receipt reading is off.");
        }
    }
}
