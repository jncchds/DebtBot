using DebtBot.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

/// <summary>
/// A personal access token (<c>dbt_…</c>) for calling the API as the user. Only the SHA-256 hash of the token is
/// stored; <see cref="Prefix"/> (the first characters after <c>dbt_</c>) is kept so the user can tell tokens apart.
/// </summary>
public class ApiToken
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(16)]
    public string Prefix { get; set; }

    [MaxLength(128)]
    public string Hash { get; set; }

    public ApiTokenScope Scope { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
