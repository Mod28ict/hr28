namespace HR28.Application.Interfaces;

/// <summary>
/// Sends SMS messages. Development uses a fake sender that writes to the log;
/// other environments need a real provider. Callers must never log message text
/// themselves, because it can contain sign-in codes.
/// </summary>
public interface ISmsSender
{
    /// <summary>Returns false if the message could not be sent.</summary>
    Task<bool> SendAsync(string phoneNumber, string message);
}
