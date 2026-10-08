namespace HR28.Domain.Entities;

/// <summary>
/// The client's own logo (Settings → System → Branding). At most one row (Id = 1).
/// Checked and rebuilt without metadata like voter photos. Shown on the sign-in pages
/// and in the sidebar; each client deployment has its own.
/// </summary>
public class BrandLogo
{
    public int Id { get; set; }

    public byte[] Content { get; set; } = Array.Empty<byte>();

    public string ContentType { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
