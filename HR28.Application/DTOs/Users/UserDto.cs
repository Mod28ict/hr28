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

    public DateTime? LastLoginAt { get; set; }
    public string? RoleName { get; set; }

    public string? ConstituencyName { get; set; }

    public string? IslandName { get; set; }
}