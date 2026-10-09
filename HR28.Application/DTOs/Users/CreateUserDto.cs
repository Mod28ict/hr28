namespace HR28.Application.DTOs.Users;

public class CreateUserDto
{
    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string MobileNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Designation { get; set; }

    public string? Remarks { get; set; }
}