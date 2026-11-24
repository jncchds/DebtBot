namespace DebtBot.Identity;

public class IdentityData
{
    public const string AdminUserClaimName = "Admin";
    public const string AdminUserPolicyName = "Admin";

    /// <summary>The default scheme: forwards to <see cref="ApiTokenScheme"/> for <c>Bearer dbt_…</c>, to JWT otherwise.</summary>
    public const string SmartScheme = "JwtOrApiToken";
    public const string ApiTokenScheme = "ApiToken";

    /// <summary>How the request was authenticated; only API tokens set it (<see cref="AuthMethodApiToken"/>).</summary>
    public const string AuthMethodClaimName = "auth_method";
    public const string AuthMethodApiToken = "pat";
    /// <summary>The API token's scope (<c>Read</c> or <c>ReadWrite</c>).</summary>
    public const string ApiTokenScopeClaimName = "pat_scope";
    /// <summary>The API token's id; the rate limit is per token.</summary>
    public const string ApiTokenIdClaimName = "pat_id";
}
