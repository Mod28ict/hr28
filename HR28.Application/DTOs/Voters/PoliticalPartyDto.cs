namespace HR28.Application.DTOs.Voters;

public class PoliticalPartyDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>The Voters list opens filtered to this party.</summary>
    public bool IsDefaultFilter { get; set; }

    /// <summary>Voters recorded with this party (a used party can't be deleted).</summary>
    public int VoterCount { get; set; }
}

public class SavePoliticalPartyDto
{
    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;
}
