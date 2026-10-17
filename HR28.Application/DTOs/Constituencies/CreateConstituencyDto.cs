using System.ComponentModel.DataAnnotations;

namespace HR28.Application.DTOs.Constituencies;

public class CreateConstituencyDto
{
    [MaxLength(10, ErrorMessage = "Code can be at most 10 characters.")]
    public string Code { get; set; } = string.Empty;
    [MaxLength(100, ErrorMessage = "Name can be at most 100 characters.")]
    public string Name { get; set; } = string.Empty;
}