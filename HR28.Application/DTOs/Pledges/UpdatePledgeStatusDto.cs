namespace HR28.Application.DTOs.Pledges;

public class UpdatePledgeStatusDto
{
    public string Status { get; set; }
        = string.Empty;

    public string ResolutionNotes { get; set; }
        = string.Empty;
}