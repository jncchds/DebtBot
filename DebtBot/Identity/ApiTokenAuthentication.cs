using DebtBot.Models.Enums;
using DebtBot.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace DebtBot.Identity;

/// <summary>
/// Authenticates <c>Authorization: Bearer dbt_…</c> (a personal API token) as its user, with the same claims as the
/// user's JWT, read from the database on every request, plus <c>auth_method=pat</c>, <c>pat_scope</c> and <c>pat_id</c>.
/// </summary>
public class ApiTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiTokenService _tokens;

    public ApiTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiTokenService tokens)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
    }

    /// <summary>The bearer value of the request, or null.</summary>
    public static string? BearerValue(HttpRequest request)
    {
        string? header = request.Headers.Authorization;
        const string prefix = "Bearer ";
        if (header is null || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return header[prefix.Length..].Trim();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var raw = BearerValue(Request);
        if (!ApiTokenService.LooksLikeApiToken(raw))
        {
            return AuthenticateResult.NoResult();
        }

        var identity = await _tokens.AuthenticateAsync(raw!, Context.RequestAborted);
        if (identity is null)
        {
            return AuthenticateResult.Fail("The API token is unknown, expired or revoked.");
        }

        var claims = IdentityService.UserClaims(identity.UserId, identity.DisplayName, identity.Role);
        claims.Add(new Claim(IdentityData.AuthMethodClaimName, IdentityData.AuthMethodApiToken));
        claims.Add(new Claim(IdentityData.ApiTokenScopeClaimName, identity.Scope.ToString()));
        claims.Add(new Claim(IdentityData.ApiTokenIdClaimName, identity.TokenId.ToString()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}

public static class ApiTokenPrincipal
{
    public static bool IsApiToken(this ClaimsPrincipal? user) =>
        user?.FindFirstValue(IdentityData.AuthMethodClaimName) == IdentityData.AuthMethodApiToken;

    public static bool IsReadOnlyApiToken(this ClaimsPrincipal? user) =>
        user.IsApiToken() && user!.FindFirstValue(IdentityData.ApiTokenScopeClaimName) != ApiTokenScope.ReadWrite.ToString();

    public static string? ApiTokenId(this ClaimsPrincipal? user) =>
        user.IsApiToken() ? user!.FindFirstValue(IdentityData.ApiTokenIdClaimName) : null;
}

/// <summary>
/// Refuses API tokens (403, whatever their scope): for token management, passwords and the web session endpoints.
/// Session (JWT) and anonymous callers pass, and the endpoint's own [Authorize] still applies.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class JwtOnlyAttribute : Attribute, IAuthorizationFilter
{
    public const string Message = "API tokens can't be used here. Sign in to the web app instead.";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.IsApiToken())
        {
            context.Result = new ObjectResult(new { message = Message, errors = new[] { Message } })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
    }
}

/// <summary>A read-only API token gets 403 on anything but GET, HEAD and OPTIONS, for every endpoint.</summary>
public class ApiTokenScopeMiddleware
{
    public const string Message = "This API token is read-only.";

    private readonly RequestDelegate _next;

    public ApiTokenScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public static bool IsReadMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.IsReadOnlyApiToken() && !IsReadMethod(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = Message, errors = new[] { Message } });
            return;
        }

        await _next(context);
    }
}
