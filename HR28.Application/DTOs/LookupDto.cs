namespace HR28.Application.DTOs;

public class LookupDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Short code, e.g. a constituency code; empty when there is none.</summary>
    public string? Code { get; set; }
}
