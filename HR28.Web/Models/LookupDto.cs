namespace HR28.Web.Models;

public class LookupDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Short code, e.g. a constituency code; empty when there is none.</summary>
    public string? Code { get; set; }
}
