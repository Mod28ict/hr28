namespace HR28.Application.DTOs.Auth;

public class LoginResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;
}