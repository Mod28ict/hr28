namespace HR28.Domain.Entities;

public class Role
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// What opens when someone with this role opens a voter: "Full" (the profile) or
    /// "AddEncounter" (straight to adding an encounter). Chosen by the Administrator.
    /// </summary>
    public string VoterProfileView { get; set; } = "Full";

    public ICollection<UserRole> UserRoles { get; set; }
        = new List<UserRole>();
}
