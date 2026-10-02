using HR28.Application.Common;

namespace HR28.API.Middleware;

/// <summary>
/// Turns expected service exceptions into clean JSON responses
/// ({ "message": "..." }) so callers never see stack traces.
/// </summary>
public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(
        RequestDelegate next,
        ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AccessDeniedException ex)
        {
            await WriteAsync(context, StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (KeyNotFoundException)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, "The requested record was not found.");
        }
        catch (BusinessRuleException ex)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled API error on {Path}", context.Request.Path);

            await WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Something went wrong. Please try again.");
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string message)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = status;

        await context.Response.WriteAsJsonAsync(new { message });
    }
}
