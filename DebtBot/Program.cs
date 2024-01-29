using DebtBot.DB;
using DebtBot.Interfaces;
using DebtBot.Interfaces.Services;
using DebtBot.Processors;
using DebtBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;

const int retryCount = 3;
const int retryDelay = 3000;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserContactService, UserContactService>();
builder.Services.AddScoped<IBillService, BillService>();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddScoped<IProcessor, BillProcessor>();

builder.Services.AddHostedService<ProcessorRunner>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Debt Bot", Version = "v1" });

    // Set the comments path for the Swagger JSON and UI.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

builder.Services.AddDbContext<DebtContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DebtBot"));
});
var app = builder.Build();

// Migrate database
for (int i = 0; i < retryCount; i++)
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
        if (i < retryCount)
        {
            Console.WriteLine($"Error migrating database: {ex.Message}");
            Console.WriteLine("Retrying...");
            Thread.Sleep(retryDelay);
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
