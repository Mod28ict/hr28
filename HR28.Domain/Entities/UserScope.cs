using HR28.Domain.Enums;

namespace HR28.Domain.Entities;

public class UserScope
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public ScopeLevel ScopeLevel { get; set; }


    public Guid? ConstituencyId { get; set; }

    public Constituency? Constituency { get; set; }

    public Guid? IslandId { get; set; }

    public Island? Island { get; set; }
}