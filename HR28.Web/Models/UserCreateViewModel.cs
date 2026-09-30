using HR28.Web.Models.Users;

namespace HR28.Web.Models;

public class UserCreateViewModel
{
    public CreateUserDto User { get; set; } = new();

    public string? GeneratedAuthorizationCode { get; set; }
}