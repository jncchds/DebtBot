using DebtBot;
using DebtBot.Controllers;
using DebtBot.Crutch;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Identity;
using DebtBot.Interfaces;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Models.Enums;
using DebtBot.Receipts;
using DebtBot.Repositories;
using DebtBot.Services;
using DebtBot.Telegram;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("DebtBot_");

// Add repositories to the container.
builder.Services.AddScoped<IBillRepository, BillRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Add services to the container.
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserContactService, UserContactService>();
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddScoped<IParserService, ParserService>();
builder.Services.AddScoped<IBillService, BillService>();
builder.Services.AddScoped<IDebtService, DebtService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IApiTokenService, ApiTokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddDebtBotReceipts(builder.Configuration);

// telegram
builder.Services.AddScoped<UpdateHandler>();
builder.Services.AddScoped<ReceiverService>();
builder.Services.AddHostedService<PollingService>();
builder.Services.AddHostedService<RefreshTokenPurgeService>();

builder.Services.AddSingleton<TelegramRateLimiter>();
builder.Services.AddSingleton<TelegramBotInfoProvider>();

builder.Services.Scan(q => q
    .FromAssemblyOf<Program>()
    .AddClasses(c =>
        c.AssignableToAny([typeof(ITelegramCommand), typeof(ITelegramCallbackQuery)]))
    .AsSelfWithInterfaces()
    .WithScopedLifetime());

builder.Services.AddHttpClient("telegram_bot_client")
                .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
                {
                    // Without a real token the client still has to be constructible (consumers depend on it); its calls just fail.
                    var botToken = builder.Configuration[$"{DebtBotConfiguration.SectionName}:Telegram:BotToken"];
                    TelegramBotClientOptions options = new(TelegramConfiguration.IsRealBotToken(botToken) ? botToken! : "0:not-configured");
                    return new TelegramBotClient(options, httpClient);
                });

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    var telegramConfiguration = builder.Configuration.GetSection($"{DebtBotConfiguration.SectionName}:Telegram").Get<TelegramConfiguration>();
    if (!string.IsNullOrWhiteSpace(telegramConfiguration!.LogBotToken) && telegramConfiguration.LogChatId is long logChatId and not 0)
    {
        logging.AddProvider(new TelegramLoggerProvider(telegramConfiguration.LogBotToken, logChatId, telegramConfiguration.LogLevel));
    }
});

builder.Services.AddMassTransit(configurator =>
{
    configurator.AddConsumers(typeof(Program).Assembly);
    configurator.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(
            builder.Configuration[$"{DebtBotConfiguration.SectionName}:RabbitMq:Host"],
            "/",
            h =>
            {
                h.Username(builder.Configuration[$"{DebtBotConfiguration.SectionName}:RabbitMq:Username"]!);
                h.Password(builder.Configuration[$"{DebtBotConfiguration.SectionName}:RabbitMq:Password"]!);
            });

        cfg.ConfigureEndpoints(context);
        cfg.ConcurrentMessageLimit = 1;
    });
});

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums go over the API as their names ("Admin", "Finalized"), not numbers. Numbers are still accepted on input.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Behind a reverse proxy (opt-in, DebtBot:ForwardedHeaders:Enabled): the client IP and scheme come from
// X-Forwarded-For / X-Forwarded-Proto, so the per-IP sign-in limit sees real clients and Request.IsHttps is right.
var forwarded = builder.Configuration
    .GetSection($"{DebtBotConfiguration.SectionName}:ForwardedHeaders")
    .Get<ForwardedHeadersConfiguration>() ?? new ForwardedHeadersConfiguration();
if (forwarded.Enabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        var proxies = SplitList(forwarded.KnownProxies).Select(IPAddress.Parse).ToList();
        var networks = SplitList(forwarded.KnownNetworks).Select(System.Net.IPNetwork.Parse).ToList();
        // Only loopback is trusted by default; a proxy in another container needs the list, or (with none given)
        // any proxy is trusted. That's only safe when the app can't be reached except through the proxy.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        proxies.ForEach(options.KnownProxies.Add);
        networks.ForEach(options.KnownIPNetworks.Add);
    });
}

builder.Services.AddDebtBotRateLimiter(builder.Configuration);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Debt Bot", Version = "v1" });

    c.OperationFilter<RawTextRequestOperationFilter>();
    c.OperationFilter<ImageUploadRequestOperationFilter>();

    // Set the comments path for the Swagger JSON and UI.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Paste a web session access token (JWT) or a personal API token (dbt_…), without the \"Bearer \" prefix. " +
            "Both work the same way. Create an API token on your Profile, under API tokens.",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT or dbt_ API token"
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });

    //c.OperationFilter<SecurityRequirementsOperationFilter>();
});

builder.Services.AddDbContext<DebtContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DebtBot"));
});

builder.Services.Configure<DebtBotConfiguration>(
    builder.Configuration.GetSection(DebtBotConfiguration.SectionName));

builder.Services.AddDebtBotAuthentication(builder.Configuration);

var app = builder.Build();

var debtBotConfig = app.Services.GetRequiredService<IOptions<DebtBotConfiguration>>().Value;

WarnAboutInsecureConfiguration(app, debtBotConfig);
app.LogReceiptStatus();

ApplyDatabaseMigrations(app.Services, debtBotConfig);

CreateDefaultUser(app.Services);

if (forwarded.Enabled)
{
    app.UseForwardedHeaders();
}

// Swagger is served in every environment, production included (user decision, series 2).
app.UseSwagger();
app.UseSwaggerUI();

// The web UI is built into wwwroot (see DebtBot/Dockerfile).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseDebtBotAuth();

app.MapControllers();

// Client-side routes get index.html. Unknown /api/* and /swagger/* paths are left alone so they still return 404.
app.MapFallbackToFile("{*path:regex(^(?!(api|swagger)(/|$)).*$):nonfile}", "index.html");

app.Run();

static IEnumerable<string> SplitList(string? value) =>
    (value ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

static void WarnAboutInsecureConfiguration(WebApplication app, DebtBotConfiguration config)
{
    var key = config.JwtConfiguration?.Key;
    if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < 32)
    {
        throw new Exception("DebtBot:JwtConfiguration:Key must be at least 32 bytes long. Set DebtBot_DebtBot__JwtConfiguration__Key.");
    }

    var defaultKey = new ConfigurationBuilder()
        .SetBasePath(app.Environment.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: true)
        .Build()[$"{DebtBotConfiguration.SectionName}:JwtConfiguration:Key"];
    if (key == defaultKey)
    {
        app.Logger.LogWarning(
            "DebtBot:JwtConfiguration:Key is still the default from appsettings.json, so anyone can sign tokens. " +
            "Set DebtBot_DebtBot__JwtConfiguration__Key to a long random secret.");
    }

    if (config.Telegram?.IsBotConfigured != true)
    {
        app.Logger.LogWarning("DebtBot:Telegram:BotToken is not set: the Telegram bot is off and Telegram sign-in is unavailable.");
    }
}

static void CreateDefaultUser(IServiceProvider services)
{
    using (var scope = services.CreateScope())
    {
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var firstAdmin = userService.GetFirstAdmin();

        if (firstAdmin != null)
        {
            Console.WriteLine("Admin user already exists, skipping default user creation");
            return;
        }

        string? defaultTelegramIdVar = Environment.GetEnvironmentVariable("DEFAULT_USER_TELEGRAM_ID");
        long? defaultTelegramId = null;
        string? defaultTelegramUserName = Environment.GetEnvironmentVariable("DEFAULT_USER_TELEGRAM_USERNAME");

        if (!string.IsNullOrWhiteSpace(defaultTelegramIdVar))
        {
            if (!long.TryParse(defaultTelegramIdVar, out var id))
            {
                throw new Exception($"Invalid telegram id {defaultTelegramIdVar}");
            }

            defaultTelegramId = id;
        }

        if (string.IsNullOrWhiteSpace(defaultTelegramUserName) && defaultTelegramId == null)
        {
            throw new Exception("No admins found and no default user provided, please fill in DEFAULT_USER_TELEGRAM_ID and/or DEFAULT_USER_TELEGRAM_USERNAME");
        }

        Console.WriteLine("Creating default user");

        var user = userService.FindOrAddUser(new DebtBot.Models.User.UserSearchModel
        {
            TelegramId = defaultTelegramId,
            TelegramUserName = defaultTelegramUserName,
            DisplayName = "Admin"
        });
        if (user == null)
        {
            throw new Exception("Failed to create default user");
        }
        if (!userService.SetRole(user.Id, UserRole.Admin))
        {
            throw new Exception("Failed to set Admin role to default user");
        }
    }
}

static void ApplyDatabaseMigrations(IServiceProvider services, DebtBotConfiguration debtBotConfig)
{
    for (int i = 0; i < debtBotConfig.Migration.RetryCount; i++)
    {
        try
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DebtContext>();
                db.Database.Migrate();
                break;
            }
        }
        catch (Exception ex)
        {
            if (i < debtBotConfig.Migration.RetryCount)
            {
                Console.WriteLine($"Error migrating database: {ex.Message}");
                Console.WriteLine("Retrying...");
                Thread.Sleep(debtBotConfig.Migration.RetryDelay);
            }
            else
            {
                throw;
            }
        }
    }
}
