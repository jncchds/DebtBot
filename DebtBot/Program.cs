using DebtBot;
using DebtBot.Crutch;
using DebtBot.DB;
using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Processors;
using DebtBot.Processors.Notification;
using DebtBot.Services;
using DebtBot.Telegram;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserContactService, UserContactService>();
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddScoped<IParserService, ParserService>();
builder.Services.AddScoped<IBillService, BillService>();
builder.Services.AddScoped<IDebtService, DebtService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IExcelService, ExcelService>();

// telegram
builder.Services.AddScoped<UpdateHandler>();
builder.Services.AddScoped<ReceiverService>();
builder.Services.AddHostedService<PollingService>();

builder.Services.AddSingleton<RateLimittingProcessor>();

builder.Services.Scan(q => q
    .FromCallingAssembly()
    .AddClasses(c =>
        c.AssignableToAny([typeof(ITelegramCommand), typeof(ITelegramCallbackQuery), typeof(INotificationProcessorBase)]))
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

builder.Services.AddControllers();
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
        Description = "Please insert JWT with Bearer into field",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
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

// Migrate database
for (int i = 0; i < debtBotConfig.Migration.RetryCount; i++)
{
    try
    {
        using (var scope = app.Services.CreateScope())
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
