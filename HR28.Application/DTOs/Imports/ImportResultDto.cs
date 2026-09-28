namespace HR28.Application.DTOs.Imports;

public class ImportResultDto
{
    public int TotalRows { get; set; }

    public int ConstituenciesCreated { get; set; }

    public int VotersInserted { get; set; }

    public int VotersUpdated { get; set; }

    public int VotersUnchanged { get; set; }

    public int Errors { get; set; }

    public List<string> ErrorMessages { get; set; }
        = new();
}