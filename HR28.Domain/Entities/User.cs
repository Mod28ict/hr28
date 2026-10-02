namespace HR28.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;

    /// <summary>
    /// Legacy plain-text code. Cleared once hashed at startup; never written for new codes.
    /// Kept only so the one-time conversion can run, then to be dropped.
    /// </summary>
    public string? AuthorizationCode { get; set; }

    /// <summary>Keyed hash of the authorization code (see IAuthorizationCodeHasher).</summary>
    public string? AuthorizationCodeHash { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsLocked { get; set; }

    public DateTime? LockedUntilUtc { get; set; }

    public int FailedLoginAttempts { get; set; }

    public string Remarks { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid CreatedBy { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; }
        = new List<UserRole>();
    public ICollection<UserScope> UserScopes { get; set; }
    = new List<UserScope>();
    public ICollection<Encounter> RecordedEncounters { get; set; }
    = new List<Encounter>();

    public ICollection<Pledge> CreatedPledges { get; set; }
    = new List<Pledge>();

    public ICollection<Pledge> AssignedPledges { get; set; }
        = new List<Pledge>();

}