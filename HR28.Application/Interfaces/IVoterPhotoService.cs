namespace HR28.Application.Interfaces;

/// <summary>
/// Voter photos (owner decision, 2026-10-05). Each call checks the right
/// (Voters.Photo.View / Voters.Photo.Edit) and that the voter is in the user's areas.
/// </summary>
public interface IVoterPhotoService
{
    /// <summary>The photo, or null when the voter has none.</summary>
    Task<(byte[] Content, string ContentType)?> GetAsync(Guid voterId);

    /// <summary>Adds or replaces the photo. Throws BusinessRuleException with a plain message if the file isn't usable.</summary>
    Task SaveAsync(Guid voterId, byte[] data);

    /// <summary>Removes the photo (nothing happens if there is none).</summary>
    Task RemoveAsync(Guid voterId);
}
