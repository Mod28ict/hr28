namespace HR28.Application.DTOs.Encounters;

public class EncounterDto
{
    public Guid Id { get; set; }

    public Guid VoterId { get; set; }

    public DateTime EncounterDate { get; set; }

    public string EncounterType { get; set; }
        = string.Empty;

    public string Outcome { get; set; }
        = string.Empty;

    public string Notes { get; set; }
        = string.Empty;

    public Guid RecordedByUserId { get; set; }
}
