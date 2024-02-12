using DebtBot;
using DebtBot.Crutch;
using DebtBot.DB;
using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserContactService, UserContactService>();
builder.Services.AddScoped<IBillService, BillService>();
builder.Services.AddScoped<IDebtService, DebtService>();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddLogging();

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

builder.Services.AddDbContextFactory<DebtContext>(options =>
{
    options.UseLazyLoadingProxies();
    options.UseNpgsql(builder.Configuration.GetConnectionString("DebtBot"));
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
