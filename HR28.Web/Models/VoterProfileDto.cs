namespace HR28.Web.Models;

public class VoterProfileDto
{
    public VoterSearchDto? Voter { get; set; }

    public List<VoterInfluencerDto> Influencers
    {
        get;
        set;
    } = new();

    public List<EncounterDto> Encounters
    {
        get;
        set;
    } = new();

    public List<PledgeDto> Pledges
    {
        get;
        set;
    } = new();
}