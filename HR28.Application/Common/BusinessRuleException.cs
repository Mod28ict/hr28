namespace HR28.Application.Common;

/// <summary>
/// A request broke a business rule (duplicate, invalid combination, out of range).
/// The message is user-friendly and safe to show. Controllers map it to 400.
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }
}
