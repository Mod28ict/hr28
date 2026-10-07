namespace HR28.Web.Models.Users;

/// <summary>One page of the Users list, plus the totals shown above it (for all users).</summary>
public class UserPageModel : PagedResult<UserDto>
{
    public int TotalUsers { get; set; }

    public int ActiveUsers { get; set; }

    public int InactiveUsers { get; set; }

    public int NoRoleUsers { get; set; }
}
