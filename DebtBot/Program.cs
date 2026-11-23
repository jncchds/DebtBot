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
using DebtBot.Repositories;
using DebtBot.Services;
using DebtBot.Telegram;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();

// telegram
builder.Services.AddScoped<UpdateHandler>();
builder.Services.AddScoped<ReceiverService>();
builder.Services.AddHostedService<PollingService>();

builder.Services.AddSingleton<TelegramRateLimiter>();
builder.Services.AddSingleton<TelegramBotInfoProvider>();

builder.Services.Scan(q => q
    .FromCallingAssembly()
    .AddClasses(c =>
        c.AssignableToAny([typeof(ITelegramCommand), typeof(ITelegramCallbackQuery)]))
    .AsSelfWithInterfaces()
    .WithScopedLifetime());

builder.Services.AddHttpClient("telegram_bot_client")
                .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
                {
                    TelegramBotClientOptions options = new(builder.Configuration[$"{DebtBotConfiguration.SectionName}:Telegram:BotToken"]!);
                    return new TelegramBotClient(options, httpClient);
                });

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    var telegramConfiguration = builder.Configuration.GetSection($"{DebtBotConfiguration.SectionName}:Telegram").Get<TelegramConfiguration>();
    if (!string.IsNullOrWhiteSpace(telegramConfiguration!.LogBotToken) && telegramConfiguration.LogChatId != 0)
    {
        logging.AddProvider(new TelegramLoggerProvider(telegramConfiguration.LogBotToken, telegramConfiguration.LogChatId, telegramConfiguration.LogLevel));
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
                h.Username(builder.Configuration[$"{DebtBotConfiguration.SectionName}:RabbitMq:Username"]);
                h.Password(builder.Configuration[$"{DebtBotConfiguration.SectionName}:RabbitMq:Password"]);
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

// Basic brute-force protection for the sign-in endpoints, per client IP.
// Behind a reverse proxy all clients share the proxy's IP unless forwarded headers are set up.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthController.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Debt Bot", Version = "v1" });

    c.OperationFilter<RawTextRequestOperationFilter>();

    // Set the comments path for the Swagger JSON and UI.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Paste the access token (without the \"Bearer \" prefix)",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    //c.OperationFilter<SecurityRequirementsOperationFilter>();
});

builder.Services.AddDbContext<DebtContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DebtBot"));
});

builder.Services.Configure<DebtBotConfiguration>(
    builder.Configuration.GetSection(DebtBotConfiguration.SectionName));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(x =>
    {
        x.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuer = builder.Configuration[$"{DebtBotConfiguration.SectionName}:JwtConfiguration:Issuer"],
            ValidAudience = builder.Configuration[$"{DebtBotConfiguration.SectionName}:JwtConfiguration:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration[$"{DebtBotConfiguration.SectionName}:JwtConfiguration:Key"]!)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(IdentityData.AdminUserPolicyName,
        policy => policy.RequireClaim(IdentityData.AdminUserClaimName));
});

var app = builder.Build();

var debtBotConfig = app.Services.GetRequiredService<IOptions<DebtBotConfiguration>>().Value;

WarnAboutInsecureConfiguration(app, debtBotConfig);

ApplyDatabaseMigrations(app.Services, debtBotConfig);

CreateDefaultUser(app.Services);

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// The web UI is built into wwwroot (see DebtBot/Dockerfile).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Client-side routes get index.html. Unknown /api/* and /swagger/* paths are left alone so they still return 404.
app.MapFallbackToFile("{*path:regex(^(?!(api|swagger)(/|$)).*$):nonfile}", "index.html");

app.Run();

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
