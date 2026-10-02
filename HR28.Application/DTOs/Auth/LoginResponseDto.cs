namespace HR28.Application.DTOs.Auth;

public class LoginResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Highest-authority role, for display.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>All roles; the web app combines their permissions.</summary>
    public List<string> Roles { get; set; } = new();
}