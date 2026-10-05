namespace HR28.Domain.Entities;

/// <summary>
/// The optional photo of a voter (owner decision, 2026-10-05). One per voter, kept in
/// the database (never in wwwroot), served only through the API to people with the
/// "View voter photos" right. Location metadata is stripped before it is stored.
/// </summary>
public class VoterPhoto
{
    public Guid VoterId { get; set; }

    public Voter Voter { get; set; } = null!;

    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>image/jpeg or image/png.</summary>
    public string ContentType { get; set; } = string.Empty;

    public int SizeBytes { get; set; }

    public DateTime UploadedAt { get; set; }

    public Guid UploadedByUserId { get; set; }
}
