namespace HR28.Application.DTOs.Users;

public class UserDto
{
    public Guid Id { get; set; }

    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;

    /// <summary>
    /// Only filled in the response to creating a user (shown once). Stored codes
    /// are hashed and can never be returned.
    /// </summary>
    public string AuthorizationCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string Remarks { get; set; } = string.Empty;

    public DateTime? LastLoginAt { get; set; }

    /// <summary>Browsers remembered with "Remember me on this device" (not expired).</summary>
    public int RememberedDevices { get; set; }
    /// <summary>The user's highest-authority role (kept for screens that show one).</summary>
    public string? RoleName { get; set; }

    /// <summary>All of the user's roles, highest authority first.</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>All of the user's areas.</summary>
    public List<UserScopeDto> Scopes { get; set; } = new();

    public string? ConstituencyName { get; set; }

    public string? IslandName { get; set; }
}