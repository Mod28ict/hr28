namespace HR28.Web.Models;

/// <summary>Outcome of one voter list upload, as returned by the API.</summary>
public class VoterImportResult
{
    public int TotalRows { get; set; }

    public int ConstituenciesCreated { get; set; }

    public int VotersInserted { get; set; }

    public int VotersUpdated { get; set; }

    public int VotersUnchanged { get; set; }

    public int Errors { get; set; }

    public bool Stopped { get; set; }

    public List<string> ErrorMessages { get; set; } = new();
}

public class VoterImportViewModel
{
    public VoterImportResult? Result { get; set; }

    public string? FileName { get; set; }

    public string? ErrorMessage { get; set; }
}
