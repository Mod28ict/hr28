using System.ComponentModel.DataAnnotations;

namespace HR28.Web.Models;

/// <summary>Quick entry: one voter found by ID card, with the add forms the user's rights allow.</summary>
public class QuickEntryViewModel
{
    /// <summary>What was typed in the ID card box.</summary>
    public string? NationalId { get; set; }

    public VoterSearchDto? Voter { get; set; }

    /// <summary>Plain message when the ID card isn't found in the user's areas.</summary>
    public string? SearchMessage { get; set; }

    public QuickEncounterForm Encounter { get; set; } = new();

    public QuickPledgeForm Pledge { get; set; } = new();

    public QuickLinkForm Link { get; set; } = new();

    public List<InfluencerDto> Influencers { get; set; } = new();

    /// <summary>Which form failed (its message shows under it, with what was typed kept).</summary>
    public string? EncounterError { get; set; }

    public string? PledgeError { get; set; }

    public string? LinkError { get; set; }
}

public class QuickEncounterForm
{
    [Required]
    public Guid VoterId { get; set; }

    [Required(ErrorMessage = "Enter the date and time.")]
    [Display(Name = "Date and time")]
    public DateTime EncounterDate { get; set; } = QuickEntryDefaults.NowToTheMinute();

    [Required(ErrorMessage = "Choose the encounter type.")]
    [Display(Name = "Encounter type")]
    public string EncounterType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose Meet, Call or Request.")]
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Only people with "Set encounter response" choose it.</summary>
    public string? Response { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; } = string.Empty;
}

public class QuickPledgeForm
{
    [Required]
    public Guid VoterId { get; set; }

    [Required(ErrorMessage = "Enter a short title for the pledge.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe what was pledged.")]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Pledge date")]
    public DateTime PledgeDate { get; set; } = QuickEntryDefaults.NowToTheMinute();

    [Display(Name = "Due date")]
    public DateTime? DueDate { get; set; }
}

public class QuickLinkForm
{
    [Required]
    public Guid VoterId { get; set; }

    [Required(ErrorMessage = "Choose an influencer.")]
    [Display(Name = "Influencer")]
    public Guid? InfluencerId { get; set; }

    [Required(ErrorMessage = "Choose the relationship.")]
    [StringLength(100)]
    [Display(Name = "Relationship")]
    public string RelationshipType { get; set; } = string.Empty;

    public static readonly string[] Relationships =
        { "Spouse", "Parent", "Child", "Sibling", "Relative", "Friend", "Colleague", "Community Leader", "Other" };
}

public static class QuickEntryDefaults
{
    /// <summary>Maldives time now, to the minute (so date boxes don't show seconds).</summary>
    public static DateTime NowToTheMinute()
    {
        var now = HR28.Web.Services.Hr28Time.Now;
        return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
    }
}
