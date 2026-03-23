# Project Guidelines

## Architecture

ASP.NET Core 8.0 Web API + Telegram Bot for shared expense tracking and debt management.

**Solution structure:**
- `DebtBot/` — Main application: API controllers, services, repositories, MassTransit consumers, Telegram bot
- `DebtBot.DB/` — EF Core data layer (PostgreSQL via Npgsql), migrations, `DebtContext`
- `DebtBot.Models/` — DTOs and view models
- `DebtBot.Models.Enums/` — Shared enums (`ProcessingState`, `ObjectType`, `UserRole`)

**Key technologies:** EF Core 8.0 + PostgreSQL, MassTransit + RabbitMQ, Telegram.Bot, AutoMapper, JWT Bearer auth, EPPlus (Excel), Polly (resilience), Scrutor (DI scanning).

**Data flow:** Controller → Service → Repository → DbContext. Async operations use MassTransit messages (Service publishes → Consumer handles).

## Build and Test

```bash
# Build
dotnet build DebtBot.sln

# Run locally (http://localhost:5098, Swagger at /swagger)
dotnet run --project DebtBot

# Add EF Core migration
cd DebtBot && dotnet ef migrations add <Name> --project ../DebtBot.DB && cd ..

# Docker (PostgreSQL + RabbitMQ + app)
docker-compose up
```

## Conventions

### Layering
- **Controllers** inherit `DebtBotControllerBase` (provides `UserId`, `IsAdmin` from JWT claims). Use `[Authorize]` at class level; `[Authorize(IdentityData.AdminUserPolicyName)]` for admin endpoints.
- **Services** implement `I*Service`, registered as scoped. Orchestrate repositories and publish MassTransit messages. Use `FindOrAddUser()` pattern for user resolution.
- **Repositories** implement `I*Repository`, registered as scoped. Use `ProjectTo<T>()` for optimal EF Core queries. Return `PagingResult<T>` for paginated data.
- **Consumers** implement `IConsumer<TMessage>` (MassTransit). Use explicit transactions for multi-step operations.

### Async
- All async methods take `CancellationToken cancellationToken` as last parameter.

### Telegram Bot
- Commands implement `ITelegramCommand`; callback queries implement `ITelegramCallbackQuery`. Both are auto-discovered via Scrutor assembly scanning.
- Pagination uses `ToInlineKeyboardButtons()` helper. Default `CountPerPage` = 6 for Telegram contexts.

### Mapping
- AutoMapper profiles in `Mappings/` folder. Use `ProjectTo<T>()` in repositories for IQueryable, `CreateMap` for standard mappings.

### Messages
- MassTransit message classes live in `Messages/`. Consumer concurrency limit is 1 (sequential processing).

### Configuration
- Bound to `DebtBotConfiguration` via `DebtBot` section in `appsettings.json`. Environment variable overrides use `DebtBot_` prefix.
