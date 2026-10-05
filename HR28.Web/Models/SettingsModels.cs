using System.ComponentModel.DataAnnotations;

namespace HR28.Web.Models;

public class SystemSettingsDto
{
    [Required(ErrorMessage = "Campaign name is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Campaign name must be between 2 and 80 characters.")]
    [Display(Name = "Campaign name")]
    public string CampaignName { get; set; } = string.Empty;

    [Range(1, 15, ErrorMessage = "OTP expiry must be between 1 and 15 minutes.")]
    [Display(Name = "OTP expiry (minutes)")]
    public int OtpExpiryMinutes { get; set; }

    [Range(3, 10, ErrorMessage = "Maximum OTP attempts must be between 3 and 10.")]
    [Display(Name = "Maximum OTP attempts")]
    public int OtpMaxAttempts { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByName { get; set; }
}

public class MyAccountDto
{
    public string FullName { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = new();

    /// <summary>Effective rights, e.g. "Influencers.Edit".</summary>
    public List<string> Permissions { get; set; } = new();

    public bool IsAdministrator { get; set; }

    /// <summary>"Full" or "AddEncounter": what opens when the user opens a voter.</summary>
    public string? VoterProfileView { get; set; }

    public List<string> Scopes { get; set; } = new();

    public DateTime? LastLoginAt { get; set; }
}

public class ConstituencyAdminDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int IslandCount { get; set; }

    public int VoterCount { get; set; }
}

public class IslandAdminDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Atoll { get; set; } = string.Empty;

    public Guid? ConstituencyId { get; set; }

    public string ConstituencyName { get; set; } = string.Empty;
}

public class ConstituencyForm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class IslandForm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Island name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Atoll is required.")]
    [StringLength(10)]
    public string Atoll { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a constituency.")]
    public Guid? ConstituencyId { get; set; }
}

public class PermissionInfo
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    /// <summary>View, Add, Edit, Delete or Link (a column of the rights grid).</summary>
    public string Action { get; set; } = string.Empty;
}

/// <summary>Input for the shared _RightsGrid partial.</summary>
public class RightsGridModel
{
    public List<PermissionInfo> Permissions { get; set; } = new();

    public HashSet<string> Selected { get; set; } = new();

    /// <summary>Form field name each ticked box posts.</summary>
    public string InputName { get; set; } = "grants";

    /// <summary>The Administrator: every box shows "Always" instead of a tick box.</summary>
    public bool AllGranted { get; set; }
}

public class RolePermissions
{
    public Guid RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public bool HasAllPermissions { get; set; }

    public List<string> Permissions { get; set; } = new();

    public string Description { get; set; } = string.Empty;

    /// <summary>Built-in roles can't be renamed or deleted.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>"Full" or "AddEncounter".</summary>
    public string VoterProfileView { get; set; } = "Full";

    public int UserCount { get; set; }
}

public class PermissionMatrix
{
    public List<PermissionInfo> Permissions { get; set; } = new();

    public List<RolePermissions> Roles { get; set; } = new();
}

public class SettingsViewModel
{
    /// <summary>appearance | account | system | geography | lists | permissions (roles &amp; rights)</summary>
    public string Tab { get; set; } = "appearance";

    public bool IsAdministrator { get; set; }

    public MyAccountDto? Account { get; set; }

    public SystemSettingsDto? System { get; set; }

    public List<ConstituencyAdminDto> Constituencies { get; set; } = new();

    public List<IslandAdminDto> Islands { get; set; } = new();

    /// <summary>Filters the islands table on the geography tab.</summary>
    public Guid? IslandConstituencyFilter { get; set; }

    /// <summary>Set when adding or editing a constituency (Id null = new).</summary>
    public ConstituencyForm? ConstituencyForm { get; set; }

    /// <summary>Set when adding or editing an island (Id null = new).</summary>
    public IslandForm? IslandForm { get; set; }

    /// <summary>Roles × rights grid (Administrator only).</summary>
    public PermissionMatrix? Permissions { get; set; }

    /// <summary>Roles &amp; rights tab: the role being edited (first role when none chosen).</summary>
    public Guid? SelectedRoleId { get; set; }

    /// <summary>Lists tab: influencer categories.</summary>
    public List<InfluencerCategoryDto> InfluencerCategories { get; set; } = new();

    /// <summary>The category being renamed on the lists tab, if any.</summary>
    public Guid? EditCategoryId { get; set; }

    /// <summary>Lists tab: political parties.</summary>
    public List<PoliticalPartyDto> Parties { get; set; } = new();

    /// <summary>The party being edited on the lists tab, if any.</summary>
    public Guid? EditPartyId { get; set; }

    public bool IsSuperAdministrator { get; set; }
}
