namespace HR28.Domain.Entities;

public class OtpRequest
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string OtpCode { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int FailedAttempts { get; set; }

    public bool IsUsed { get; set; }

    /// <summary>
    /// What the code is for: <see cref="SignInPurpose"/> or <see cref="StepUpPurpose"/>
    /// (confirming a sensitive action while signed in). A code only works for its purpose.
    /// </summary>
    public string Purpose { get; set; } = SignInPurpose;

    public const string SignInPurpose = "SignIn";
    public const string StepUpPurpose = "StepUp";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}