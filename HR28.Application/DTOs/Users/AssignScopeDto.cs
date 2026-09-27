namespace HR28.Application.DTOs.Users;

public class AssignScopeDto
{
    public Guid UserId { get; set; }

    public Guid? ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }
}