namespace HR28.Application.DTOs.Islands;

public class CreateIslandDto
{
    public string Name { get; set; } = string.Empty;
    public string Atoll { get; set; } = string.Empty;
    public Guid ConstituencyId { get; set; }
}