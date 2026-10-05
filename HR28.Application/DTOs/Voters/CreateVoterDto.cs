namespace HR28.Application.DTOs.Voters;

public class CreateVoterDto
{
    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public Guid ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    /// <summary>Empty = "Not known".</summary>
    public Guid? PoliticalPartyId { get; set; }

    public string Remarks { get; set; } = string.Empty;
    public string SupportStatus { get; set; }
    = "Undecided";

}