namespace HR28.Application.Common;

/// <summary>
/// Thrown when a user acts on a record outside their scope or role.
/// Controllers map it to 403 Forbidden.
/// </summary>
public class AccessDeniedException : Exception
{
    public AccessDeniedException(string message)
        : base(message)
    {
    }
}
