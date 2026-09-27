using HR28.Domain.Entities;

public class Island
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid ConstituencyId { get; set; }

    public Constituency Constituency { get; set; } = null!;

    public ICollection<UserScope> UserScopes { get; set; }
        = new List<UserScope>();
}