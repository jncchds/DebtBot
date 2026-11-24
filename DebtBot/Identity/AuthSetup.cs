using DebtBot.Controllers;
using DebtBot.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

namespace DebtBot.Identity;

/// <summary>Authentication, authorization and rate limits, shared by Program.cs and the tests' host.</summary>
public static class AuthSetup
{
    public const string RateLimitMessage = "Too many requests. Try again in a minute.";

    /// <summary>
    /// The default scheme picks by the bearer value: <c>dbt_…</c> is a personal API token, anything else a JWT.
    /// </summary>
    public static IServiceCollection AddDebtBotAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(DebtBotConfiguration.SectionName);
        services
            .AddAuthentication(IdentityData.SmartScheme)
            .AddPolicyScheme(IdentityData.SmartScheme, "JWT or API token", options =>
            {
                options.ForwardDefaultSelector = SelectScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(IdentityData.ApiTokenScheme, null)
            .AddJwtBearer(x =>
            {
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = section["JwtConfiguration:Issuer"],
                    ValidAudience = section["JwtConfiguration:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section["JwtConfiguration:Key"]!)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(IdentityData.AdminUserPolicyName,
                policy => policy.RequireClaim(IdentityData.AdminUserClaimName));
        });

        return services;
    }

    /// <summary>The scheme a request goes to: <see cref="IdentityData.ApiTokenScheme"/> for <c>Bearer dbt_…</c>, JWT otherwise.</summary>
    public static string SelectScheme(HttpContext context) =>
        ApiTokenService.LooksLikeApiToken(ApiTokenAuthenticationHandler.BearerValue(context.Request))
            ? IdentityData.ApiTokenScheme
            : JwtBearerDefaults.AuthenticationScheme;

    /// <summary>
    /// The sign-in limit per client IP (policy <see cref="AuthController.RateLimitPolicy"/>), and a fixed window per API
    /// token for every request made with one (<c>DebtBot:ApiTokens:RateLimitPerMinute</c>). Session (JWT) traffic
    /// isn't limited. Behind a reverse proxy all clients share the proxy's IP unless forwarded headers are enabled.
    /// </summary>
    public static IServiceCollection AddDebtBotRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        var apiTokenLimit = configuration.GetValue<int?>($"{DebtBotConfiguration.SectionName}:ApiTokens:RateLimitPerMinute")
            ?? new ApiTokenConfiguration().RateLimitPerMinute;

        services.AddRateLimiter(options =>
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

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var tokenId = httpContext.User.ApiTokenId();
                return tokenId is null || apiTokenLimit <= 0
                    ? RateLimitPartition.GetNoLimiter("")
                    : RateLimitPartition.GetFixedWindowLimiter("pat:" + tokenId, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = apiTokenLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }
                await context.HttpContext.Response.WriteAsJsonAsync(new { message = RateLimitMessage }, cancellationToken);
            };
        });

        return services;
    }

    /// <summary>Authentication, then the rate limits (which need to know the token), the read-only scope, and authorization.</summary>
    public static IApplicationBuilder UseDebtBotAuth(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseMiddleware<ApiTokenScopeMiddleware>();
        app.UseAuthorization();
        return app;
    }
}
