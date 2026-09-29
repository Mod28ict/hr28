namespace HR28.Web.Models;

public class VoterCreateViewModel
{
    public VoterCreateEditDto Voter { get; set; }
        = new();

    public List<LookupDto> Constituencies { get; set; }
        = new();

    public List<LookupDto> Islands { get; set; }
        = new();
}
