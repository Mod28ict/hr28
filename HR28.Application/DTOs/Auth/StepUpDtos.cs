namespace HR28.Application.DTOs.Auth;

/// <summary>Result of sending a "confirm it's you" code.</summary>
public class StepUpSendResultDto
{
    public bool Success { get; set; }

    public bool IsThrottled { get; set; }

    public int RetryAfterSeconds { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Last digits of the number the code went to, e.g. "•••234".</summary>
    public string MaskedMobile { get; set; } = string.Empty;

    public int ExpiresInSeconds { get; set; }
}

public class StepUpVerifyRequestDto
{
    public string Code { get; set; } = string.Empty;
}

/// <summary>Result of entering the code: a new sign-in token that covers sensitive actions for a while.</summary>
public class StepUpVerifyResultDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public int ValidForSeconds { get; set; }
}
