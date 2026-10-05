namespace HR28.Web.Models;

public class VoterProfileDto
{
    public VoterSearchDto? Voter { get; set; }

    /// <summary>True when the voter has a photo and the user may view it.</summary>
    public bool HasPhoto { get; set; }

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