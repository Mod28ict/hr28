
using HR28.Domain.Entities;

namespace HR28.Domain.Entities;

public class UserScope 
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public User User { get; set; } = null!;

    public Constituency? Constituency { get; set; }

    public Island? Island { get; set; }
}