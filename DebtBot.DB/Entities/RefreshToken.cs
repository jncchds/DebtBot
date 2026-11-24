using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

/// <summary>
/// A web sign-in refresh token. Only the SHA-256 hash of the token is stored.
/// Tokens are rotated on every use: the old row gets <see cref="RevokedAt"/> and <see cref="ReplacedById"/>.
/// </summary>
public class RefreshToken
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; }

    [MaxLength(128)]
    public string TokenHash { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>The token that replaced this one when it was rotated.</summary>
    public Guid? ReplacedById { get; set; }
}
