using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace HR28.Web.Filters;

/// <summary>
/// One place that turns API failures into what the user should see, so no
/// page ever shows a raw error:
/// session ended → sign in again; no permission → dashboard with a message;
/// anything else (too many requests, service down) → the friendly error page.
/// Requests from page scripts get a JSON message with the same status instead.
/// </summary>
public class ApiFailureExceptionFilter : IExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly IModelMetadataProvider _metadataProvider;

    public ApiFailureExceptionFilter(
        ITempDataDictionaryFactory tempDataFactory,
        IModelMetadataProvider metadataProvider)
    {
        _tempDataFactory = tempDataFactory;
        _metadataProvider = metadataProvider;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApiCallException failure)
            return;

        var httpContext = context.HttpContext;

        if (failure.IsUnauthorized)
            httpContext.Session.Clear();

        if (IsScriptRequest(httpContext.Request))
        {
            context.Result = new ObjectResult(new { message = failure.UserMessage })
            {
                StatusCode = (int)failure.StatusCode
            };
        }
        else if (failure.IsUnauthorized)
        {
            context.Result = new RedirectToActionResult("Login", "Auth", null);
        }
        else if (failure.IsForbidden && !IsDashboard(context))
        {
            // Shown once by the layout on the dashboard.
            _tempDataFactory.GetTempData(httpContext)["FlashError"] = failure.UserMessage;
            context.Result = new RedirectToActionResult("Index", "Dashboard", null);
        }
        else
        {
            context.Result = new ViewResult
            {
                ViewName = "Error",
                StatusCode = (int)failure.StatusCode,
                ViewData = new ViewDataDictionary<ErrorViewModel>(_metadataProvider, context.ModelState)
                {
                    Model = new ErrorViewModel { Message = failure.UserMessage }
                }
            };
        }

        context.ExceptionHandled = true;
    }

    private static bool IsDashboard(ExceptionContext context) =>
        string.Equals(
            context.RouteData.Values["controller"]?.ToString(), "Dashboard",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsScriptRequest(HttpRequest request) =>
        request.Headers.XRequestedWith == "XMLHttpRequest" ||
        request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
}
