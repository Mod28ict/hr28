namespace HR28.Application.DTOs.Encounters;

public class CreateEncounterDto
{
    public Guid VoterId { get; set; }

    public DateTime EncounterDate { get; set; }
        = DateTime.UtcNow;

    public string EncounterType { get; set; }
        = string.Empty;

    public string Outcome { get; set; }
        = string.Empty;

    public string Notes { get; set; }
        = string.Empty;
}