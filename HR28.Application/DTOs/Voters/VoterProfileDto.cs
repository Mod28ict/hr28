using HR28.Application.DTOs.Encounters;
using HR28.Application.DTOs.Influencers;
using HR28.Application.DTOs.Pledges;

namespace HR28.Application.DTOs.Voters;

public class VoterProfileDto
{
    public VoterDto Voter { get; set; } = null!;

    public List<VoterInfluencerDto> Influencers { get; set; }
        = new();

    public List<EncounterDto> Encounters { get; set; }
        = new();

    public List<PledgeDto> Pledges { get; set; }
        = new();
    public string ConstituencyName { get; set; } = string.Empty;

    public string IslandName { get; set; } = string.Empty;
}