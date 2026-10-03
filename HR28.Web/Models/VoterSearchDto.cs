namespace HR28.Web.Models;

public class VoterSearchDto
{
    public Guid Id { get; set; }

    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public Guid? ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }
    public string ConstituencyName { get; set; }
        = string.Empty;

    public string IslandName { get; set; }
        = string.Empty;

    public string Remarks { get; set; } = string.Empty;

    public string SupportStatus { get; set; } = string.Empty;

    public int PledgeCount { get; set; }

    public string ConstituencyCode { get; set; } = string.Empty;

    /// <summary>"Kendhoo Dhaaira (F03)", or just the name when there is no code.</summary>
    public string ConstituencyLabel =>
        string.IsNullOrWhiteSpace(ConstituencyCode) ? ConstituencyName : $"{ConstituencyName} ({ConstituencyCode.Trim()})";

}
