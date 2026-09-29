namespace HR28.Web.Models;

public class LinkInfluencerViewModel
{
    public LinkInfluencerDto Link { get; set; }
        = new();

    public List<InfluencerDto> Influencers { get; set; }
        = new();
}