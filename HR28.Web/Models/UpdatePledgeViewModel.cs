using System.ComponentModel.DataAnnotations;

namespace HR28.Web.Models;

public class UpdatePledgeViewModel
{
    /// <summary>The pledge as it is now (shown for context; not posted back).</summary>
    public PledgeDto Pledge { get; set; } = new();

    [Required(ErrorMessage = "Please choose a status.")]
    public string Status { get; set; } = "Open";

    [StringLength(1000, ErrorMessage = "Resolution notes must be 1000 characters or fewer.")]
    [Display(Name = "Resolution notes")]
    public string? ResolutionNotes { get; set; }

    /// <summary>Statuses the reports understand, in workflow order.</summary>
    public static readonly (string Value, string Tone, string Help)[] Statuses =
    {
        ("Open", "is-amber", "Promised, nothing done yet"),
        ("In Progress", "is-cyan", "Work has started"),
        ("Completed", "is-green", "Delivered to the voter"),
        ("Cancelled", "is-red", "No longer being pursued")
    };
}
