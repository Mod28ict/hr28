namespace HR28.Web.Models;

public class VoterSearchDto
{
    public Guid Id { get; set; }

    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public Guid? ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public Guid? PoliticalPartyId { get; set; }

    /// <summary>Empty when the party is not known.</summary>
    public string PartyName { get; set; } = string.Empty;

    public string PartyShortName { get; set; } = string.Empty;
    public string ConstituencyName { get; set; }
        = string.Empty;

    public string IslandName { get; set; }
        = string.Empty;

    public string Remarks { get; set; } = string.Empty;

    public string SupportStatus { get; set; } = string.Empty;

    public int PledgeCount { get; set; }

    public string ConstituencyCode { get; set; } = string.Empty;

    /// <summary>"M", "F" or empty.</summary>
    public string Gender { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public string GenderLabel => Gender switch { "M" => "Male", "F" => "Female", _ => string.Empty };

    /// <summary>Age in whole years today (Maldives time), or null without a date of birth.</summary>
    public int? Age
    {
        get
        {
            if (DateOfBirth is not { } dob)
                return null;

            var today = DateOnly.FromDateTime(HR28.Web.Services.Hr28Time.Now);
            var age = today.Year - dob.Year;

            return dob > today.AddYears(-age) ? age - 1 : age;
        }
    }

    /// <summary>"Male · 52 years", "Female", "41 years" or empty.</summary>
    public string GenderAndAge =>
        string.Join(" · ", new[] { GenderLabel, Age.HasValue ? $"{Age} years" : string.Empty }.Where(s => s.Length > 0));

    /// <summary>"Kendhoo Dhaaira (F03)", or just the name when there is no code.</summary>
    public string ConstituencyLabel =>
        string.IsNullOrWhiteSpace(ConstituencyCode) ? ConstituencyName : $"{ConstituencyName} ({ConstituencyCode.Trim()})";

}
