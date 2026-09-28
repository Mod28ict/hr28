namespace HR28.Domain.Entities;

public class ConstituencyIsland
{
    public Guid ConstituencyId { get; set; }

    public Guid IslandId { get; set; }

    public Constituency Constituency { get; set; }
        = null!;

    public Island Island { get; set; }
        = null!;
}