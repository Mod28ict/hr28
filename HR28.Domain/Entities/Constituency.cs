using HR28.Domain.Entities;

public class Constituency
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Island> Islands { get; set; }
        = new List<Island>();

    public ICollection<UserScope> UserScopes { get; set; }
        = new List<UserScope>();
}