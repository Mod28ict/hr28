namespace HR28.Web.Models.Users;

public class UserAccessViewModel
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } =
        string.Empty;

    public Guid? RoleId { get; set; }

    public Guid? ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public List<RoleDto> Roles { get; set; } =
        new();

    public List<LookupDto> Constituencies
    {
        get;
        set;
    } = new();

    public List<LookupDto> Islands { get; set; } =
        new();
}
