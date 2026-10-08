using HR28.Web.Models.Users;

namespace HR28.Web.Models;

public class UserCreateViewModel
{
    public CreateUserDto User { get; set; } = new();

    public string? GeneratedAuthorizationCode { get; set; }

    /// <summary>Set when editing an existing user (the same form adds and edits).</summary>
    public Guid? EditId { get; set; }

    public bool IsEdit => EditId.HasValue;

    /// <summary>Editing only: false signs the person out on their next request.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Editing only: browsers remembered with "Remember me on this device".</summary>
    public int RememberedDevices { get; set; }

    /// <summary>Editing only: last successful sign-in (UTC).</summary>
    public DateTime? LastLoginAt { get; set; }
}