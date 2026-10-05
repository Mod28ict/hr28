using System.ComponentModel.DataAnnotations;

namespace HR28.Web.Models.Users;

public class CreateUserDto
{
    [Required(ErrorMessage = "Enter the National ID.")]
    [StringLength(7, ErrorMessage = "National ID is 7 characters, e.g. A123456.")]
    [RegularExpression(@"^[A-Za-z]\d{6}$", ErrorMessage = "National ID must be one letter followed by 6 digits, e.g. A123456.")]
    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the mobile number. The sign-in code is sent to it.")]
    [StringLength(7, ErrorMessage = "Mobile number is 7 digits.")]
    [RegularExpression(@"^\d{7}$", ErrorMessage = "Mobile number must be exactly 7 digits, e.g. 7771234.")]
    public string MobileNumber { get; set; } = string.Empty;

    public string? Email { get; set; } = string.Empty;

    public string? Designation { get; set; } = string.Empty;

    public string? Remarks { get; set; } = string.Empty;
}