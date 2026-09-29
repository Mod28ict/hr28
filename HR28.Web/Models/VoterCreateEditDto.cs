namespace HR28.Web.Models;

public class VoterCreateEditDto
{
    public Guid? Id { get; set; }

    public string NationalId { get; set; } = "";

    public string FullName { get; set; } = "";

    public string Address { get; set; } = "";

    public string MobileNumber { get; set; } = "";

    public Guid ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public string Remarks { get; set; } = "";

    public string SupportStatus { get; set; } = "";
}