using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Identity;
using DebtBot.Interfaces;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using DebtBot.Services;
using DebtBot.Telegram;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace DebtBot.Tests;

/// <summary>
/// The real controllers, authentication (the JWT-or-API-token policy scheme), rate limits and the read-only scope
/// middleware, in an in-process test server over <see cref="SqliteFixture"/>'s database.
/// </summary>
public sealed class ApiTokenHost : IAsyncDisposable
{
    private const string JwtKey = "TestsOnlyKeyTestsOnlyKeyTestsOnlyKeyTestsOnlyKey";

    private readonly WebApplication _app;

    public SqliteFixture F { get; } = new();
    public ManualTimeProvider Time { get; } = new() { Now = DateTimeOffset.UtcNow };
    public IAuthService Auth { get; } = Substitute.For<IAuthService>();
    public ApiTokenService Tokens { get; }
    public IdentityService Identity { get; }
    public HttpClient Client { get; }

    private ApiTokenHost(int rateLimitPerMinute)
    {
        var settings = new Dictionary<string, string?>
        {
            ["DebtBot:JwtConfiguration:Issuer"] = "DebtBot",
            ["DebtBot:JwtConfiguration:Audience"] = "DebtBot",
            ["DebtBot:JwtConfiguration:Key"] = JwtKey,
            ["DebtBot:JwtConfiguration:LifeTime"] = "60",
            ["DebtBot:ApiTokens:RateLimitPerMinute"] = rateLimitPerMinute.ToString(),
            ["DebtBot:ApiTokens:MaxActivePerUser"] = "20",
        };

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(BillService).Assembly.GetName().Name,
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.Configure<DebtBotConfiguration>(builder.Configuration.GetSection(DebtBotConfiguration.SectionName));

        var options = Options.Create(builder.Configuration.GetSection(DebtBotConfiguration.SectionName).Get<DebtBotConfiguration>()!);
        Tokens = new ApiTokenService(F.Db, Time, options);
        Identity = new IdentityService(options);

        var s = builder.Services;
        s.AddSingleton<DebtContext>(F.Db);
        s.AddSingleton<TimeProvider>(Time);
        s.AddSingleton<IApiTokenService>(Tokens);
        s.AddSingleton<IIdentityService>(Identity);
        s.AddSingleton<IUserService>(F.UserService);
        s.AddSingleton<IUserContactService>(F.UserContactService);
        s.AddSingleton<IBillService>(F.BillService);
        s.AddSingleton(Auth);
        s.AddSingleton(Substitute.For<IExcelService>());
        s.AddSingleton(Substitute.For<IParserService>());
        s.AddSingleton(Substitute.For<IDebtService>());
        s.AddSingleton(Substitute.For<IBudgetService>());
        s.AddSingleton<TelegramBotInfoProvider>();
        s.AddControllers()
            .AddApplicationPart(typeof(BillService).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        s.AddDebtBotRateLimiter(builder.Configuration);
        s.AddDebtBotAuthentication(builder.Configuration);

        _app = builder.Build();
        _app.UseDebtBotAuth();
        _app.MapControllers();
        _app.StartAsync().GetAwaiter().GetResult();
        Client = _app.GetTestClient();
    }

    public static ApiTokenHost Start(int rateLimitPerMinute = 10_000) => new(rateLimitPerMinute);

    public string Jwt(User user) => Identity.GenerateJwtToken(new UserModel
    {
        Id = user.Id,
        DisplayName = user.DisplayName,
        Role = user.Role,
    });

    /// <summary>Makes a token through the service and returns its secret.</summary>
    public string Pat(User user, ApiTokenScope scope = ApiTokenScope.ReadWrite, int? expiresInDays = null)
    {
        var result = Tokens.CreateAsync(user.Id, new Models.Profile.ApiTokenCreationModel
        {
            Name = "test",
            Scope = scope,
            ExpiresInDays = expiresInDays,
        }).GetAwaiter().GetResult();
        Assert.Equal(ApiTokenCreateStatus.Created, result.Status);
        F.Clear();
        return result.Token!.Token;
    }

    public Task<HttpResponseMessage> Send(HttpMethod method, string url, string? bearer, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        if (body is not null)
        {
            request.Content = System.Net.Http.Json.JsonContent.Create(body);
        }
        else if (method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options)
        {
            request.Content = System.Net.Http.Json.JsonContent.Create(new { });
        }
        return Client.SendAsync(request);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        F.Dispose();
    }
}
