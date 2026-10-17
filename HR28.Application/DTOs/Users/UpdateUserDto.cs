using System.ComponentModel.DataAnnotations;

namespace HR28.Application.DTOs.Users;

/// <summary>Edit a user's details (Users → Edit). Roles and areas are changed on their own pages.</summary>
public class UpdateUserDto
{
    [MaxLength(20, ErrorMessage = "National ID can be at most 20 characters.")]
    public string NationalId { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Full name can be at most 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(250, ErrorMessage = "Address can be at most 250 characters.")]
    public string? Address { get; set; }

    [MaxLength(20, ErrorMessage = "Mobile number can be at most 20 characters.")]
    public string MobileNumber { get; set; } = string.Empty;

    [MaxLength(254, ErrorMessage = "Email can be at most 254 characters.")]
    public string? Email { get; set; }

    [MaxLength(100, ErrorMessage = "Designation can be at most 100 characters.")]
    public string? Designation { get; set; }

    [MaxLength(1000, ErrorMessage = "Remarks can be at most 1000 characters.")]
    public string? Remarks { get; set; }

    /// <summary>False signs the person out on their next request.</summary>
    public bool IsActive { get; set; } = true;
}
