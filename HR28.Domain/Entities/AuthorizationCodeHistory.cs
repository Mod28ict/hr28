namespace HR28.Domain.Entities;

public class AuthorizationCodeHistory
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string AuthorizationCodeHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string Reason { get; set; } = string.Empty;
}