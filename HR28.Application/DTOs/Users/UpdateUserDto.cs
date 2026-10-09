namespace HR28.Application.DTOs.Users;

/// <summary>Edit a user's details (Users → Edit). Roles and areas are changed on their own pages.</summary>
public class UpdateUserDto
{
    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string MobileNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Designation { get; set; }

    public string? Remarks { get; set; }

    /// <summary>False signs the person out on their next request.</summary>
    public bool IsActive { get; set; } = true;
}
