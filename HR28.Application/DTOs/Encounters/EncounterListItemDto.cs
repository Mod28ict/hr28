namespace HR28.Application.DTOs.Encounters;

/// <summary>One row of the Encounters list: the encounter plus who it was with and who recorded it.</summary>
public class EncounterListItemDto
{
    public Guid Id { get; set; }

    public DateTime EncounterDate { get; set; }

    public string EncounterType { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string? Response { get; set; }

    public string Notes { get; set; } = string.Empty;

    public Guid VoterId { get; set; }

    public string VoterName { get; set; } = string.Empty;

    public string VoterNationalId { get; set; } = string.Empty;

    public string IslandName { get; set; } = string.Empty;

    public string ConstituencyName { get; set; } = string.Empty;

    public string RecordedBy { get; set; } = string.Empty;
}

/// <summary>Filters for the Encounters list; always applied inside the user's areas.</summary>
public class EncounterListFilter
{
    /// <summary>Matches voter name, voter National ID or the notes.</summary>
    public string? SearchTerm { get; set; }

    public string? EncounterType { get; set; }

    public string? Outcome { get; set; }

    public string? Response { get; set; }
}
