using DebtBot.Models.Enums;

namespace DebtBot.Models.Profile;

public enum ApiTokenStatus
{
    Active = 0,
    Expired = 1,
    Revoked = 2
}

/// <summary>One of your API tokens. Never carries the secret or its hash.</summary>
public class ApiTokenModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>The first 8 characters after <c>dbt_</c>, to show as <c>dbt_a1b2c3d4…</c>.</summary>
    public string Prefix { get; set; } = "";
    public ApiTokenScope Scope { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    /// <summary>Null: never expires.</summary>
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public ApiTokenStatus Status { get; set; }
}

/// <summary>The answer to creating a token: the record plus the secret, which is shown this once.</summary>
public class ApiTokenCreatedModel : ApiTokenModel
{
    public string Token { get; set; } = "";
}

/// <summary>A new token. <c>expiresInDays</c> is 30, 90, 365 or null (never).</summary>
public class ApiTokenCreationModel
{
    public string Name { get; set; } = "";
    public ApiTokenScope Scope { get; set; } = ApiTokenScope.Read;
    public int? ExpiresInDays { get; set; }
}

/// <summary>The answer to an admin's revoke-all.</summary>
public class ApiTokensRevokedModel
{
    public int Revoked { get; set; }
}
