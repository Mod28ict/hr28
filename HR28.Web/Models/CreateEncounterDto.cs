using System.ComponentModel.DataAnnotations;

namespace HR28.Web.Models;

public class CreateEncounterDto
{
    [Required]
    public Guid VoterId { get; set; }

    [Required]
    [Display(Name = "Encounter Date")]
    public DateTime EncounterDate { get; set; }
        = DateTime.Now;

    [Required]
    [Display(Name = "Encounter Type")]
    public string EncounterType { get; set; }
        = string.Empty;

    [Required]
    public string Outcome { get; set; }
        = string.Empty;

    [StringLength(1000)]
    public string Notes { get; set; }
        = string.Empty;
}