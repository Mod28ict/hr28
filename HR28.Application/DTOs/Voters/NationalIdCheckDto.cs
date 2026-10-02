namespace HR28.Application.DTOs.Voters;

public class NationalIdCheckDto
{
    public bool Exists { get; set; }

    /// <summary>Set only when the existing voter is inside the caller's scope.</summary>
    public Guid? VoterId { get; set; }

    /// <summary>Set only when the existing voter is inside the caller's scope.</summary>
    public string? FullName { get; set; }
}
