using System.Net;

namespace HR28.Web.Services;

/// <summary>
/// An API call failed in a way the page cannot handle itself (session ended,
/// no permission, too many requests, service down). Turned into a sign-in
/// redirect or a friendly page by <see cref="Filters.ApiFailureExceptionFilter"/>.
/// </summary>
public class ApiCallException : Exception
{
    public ApiCallException(HttpStatusCode statusCode, string userMessage)
        : base($"API call failed with status {(int)statusCode}.")
    {
        StatusCode = statusCode;
        UserMessage = userMessage;
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>Plain-language message that is safe to show.</summary>
    public string UserMessage { get; }

    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
}
