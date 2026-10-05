using System.ComponentModel.DataAnnotations;

namespace HR28.Web.Models;

public class CreateEncounterDto
{
    /// <summary>Set when editing an existing encounter (the same form is used for both).</summary>
    public Guid? EncounterId { get; set; }


    [Required]
    public Guid VoterId { get; set; }

    [Required]
    [Display(Name = "Encounter Date")]
    public DateTime EncounterDate { get; set; }
        = HR28.Web.Services.Hr28Time.Now;

    [Required]
    [Display(Name = "Encounter Type")]
    public string EncounterType { get; set; }
        = string.Empty;

    [Required(ErrorMessage = "Choose Meet, Call or Request.")]
    public string Outcome { get; set; }
        = string.Empty;

    [Required(ErrorMessage = "Choose the voter's response.")]
    public string? Response { get; set; }

    [StringLength(1000)]
    public string Notes { get; set; }
        = string.Empty;
}