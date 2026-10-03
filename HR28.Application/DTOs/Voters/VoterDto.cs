namespace HR28.Application.DTOs.Voters;

public class VoterDto
{
    public Guid Id { get; set; }

    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public Guid ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public string Remarks { get; set; } = string.Empty;
    /// <summary>Constituency code, e.g. "F03" (filled on the voter list; may be empty).</summary>
    public string ConstituencyCode { get; set; } = string.Empty;

    /// <summary>Number of pledges recorded for this voter (filled on the voter list).</summary>
    public int PledgeCount { get; set; }

    public string SupportStatus { get; set; }
    = string.Empty;
    public string ConstituencyName { get; set; }
        = string.Empty;

    public string IslandName { get; set; }
        = string.Empty;

}