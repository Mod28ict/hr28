namespace HR28.Web.Models;

public class EncounterDto
{
    public Guid Id { get; set; }

    public Guid VoterId { get; set; }

    public Guid RecordedByUserId { get; set; }

    public DateTime EncounterDate { get; set; }

    public string EncounterType { get; set; }
        = string.Empty;

    public string Outcome { get; set; }
        = string.Empty;

    public string? Response { get; set; }

    public string Notes { get; set; }
        = string.Empty;
}