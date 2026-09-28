

namespace HR28.Domain.Entities;

public class Encounter 
{
    public Guid Id { get; set; }

    public Guid VoterId { get; set; }

    public Guid RecordedByUserId { get; set; }

    public DateTime EncounterDate { get; set; }

    public string EncounterType { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public Voter Voter { get; set; } = null!;

    public User RecordedByUser { get; set; } = null!;
}