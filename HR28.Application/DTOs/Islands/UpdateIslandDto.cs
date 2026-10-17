using System.ComponentModel.DataAnnotations;

namespace HR28.Application.DTOs.Islands;

public class UpdateIslandDto
{
    [MaxLength(100, ErrorMessage = "Name can be at most 100 characters.")]
    public string Name { get; set; } = string.Empty;
    [MaxLength(20, ErrorMessage = "Atoll can be at most 20 characters.")]
    public string Atoll { get; set; } = string.Empty;
    public Guid ConstituencyId { get; set; }
}