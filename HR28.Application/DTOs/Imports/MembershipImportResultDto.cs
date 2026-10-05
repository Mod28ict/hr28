namespace HR28.Application.DTOs.Imports;

/// <summary>Outcome of one party membership list upload.</summary>
public class MembershipImportResultDto
{
    public string PartyName { get; set; } = string.Empty;

    /// <summary>Data rows in the file (header excluded, blank rows skipped).</summary>
    public int TotalRows { get; set; }

    /// <summary>Rows with no National ID: they can't be matched safely, so they are skipped.</summary>
    public int WithoutNationalId { get; set; }

    /// <summary>Rows whose National ID is not in the right format.</summary>
    public int InvalidNationalId { get; set; }

    /// <summary>National IDs that appear more than once (the first row is used).</summary>
    public int DuplicateRows { get; set; }

    /// <summary>Members found in the voter registry.</summary>
    public int Matched { get; set; }

    /// <summary>Members not in the registry (listed in NotInRegistry, first 100).</summary>
    public int NotInRegistryCount { get; set; }

    /// <summary>Matched voters newly recorded with the party.</summary>
    public int PartySet { get; set; }

    /// <summary>Of those, voters who were recorded with another party before.</summary>
    public int MovedFromOtherParty { get; set; }

    /// <summary>Matched voters already recorded with the party.</summary>
    public int AlreadyMembers { get; set; }

    public int DateOfBirthFilled { get; set; }

    public int MobileFilled { get; set; }

    public int GenderFilled { get; set; }

    /// <summary>Gender in the file differs from the registry; the registry is kept.</summary>
    public int GenderConflictCount { get; set; }

    public List<string> NotInRegistry { get; set; } = new();

    public List<string> GenderConflicts { get; set; } = new();

    /// <summary>Set when the file couldn't be used; nothing was changed.</summary>
    public string? ErrorMessage { get; set; }
}
