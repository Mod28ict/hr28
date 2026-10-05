namespace HR28.Web.Models;

/// <summary>Outcome of one party membership list upload, as returned by the API.</summary>
public class MembershipImportResult
{
    public string PartyName { get; set; } = string.Empty;

    public int TotalRows { get; set; }

    public int WithoutNationalId { get; set; }

    public int InvalidNationalId { get; set; }

    public int DuplicateRows { get; set; }

    public int Matched { get; set; }

    public int NotInRegistryCount { get; set; }

    public int PartySet { get; set; }

    public int MovedFromOtherParty { get; set; }

    public int AlreadyMembers { get; set; }

    public int DateOfBirthFilled { get; set; }

    public int MobileFilled { get; set; }

    public int GenderFilled { get; set; }

    public int GenderConflictCount { get; set; }

    public List<string> NotInRegistry { get; set; } = new();

    public List<string> GenderConflicts { get; set; } = new();

    public string? ErrorMessage { get; set; }
}

public class MembershipImportViewModel
{
    public List<PoliticalPartyDto> Parties { get; set; } = new();

    /// <summary>The party chosen (defaults to the party the Voters list opens on).</summary>
    public Guid? PartyId { get; set; }

    public MembershipImportResult? Result { get; set; }

    public string? FileName { get; set; }

    public string? ErrorMessage { get; set; }
}
