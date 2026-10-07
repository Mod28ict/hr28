using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace HR28.Web.Filters;

/// <summary>
/// When a form fails the anti-forgery check (a page left open too long, or a form that
/// couldn't be read, e.g. a file over the size limit), go back to the page with a plain
/// message instead of the browser's empty "400 Bad Request" page. Nothing is saved.
/// </summary>
public class FriendlyAntiforgeryFailureFilter : IAlwaysRunResultFilter
{
    private readonly ITempDataDictionaryFactory _tempDataFactory;

    public FriendlyAntiforgeryFailureFilter(ITempDataDictionaryFactory tempDataFactory)
    {
        _tempDataFactory = tempDataFactory;
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not IAntiforgeryValidationFailedResult)
            return;

        var http = context.HttpContext;

        var tempData = _tempDataFactory.GetTempData(http);
        // A large upload that went over the size limit, or an ordinary form that expired.
        var largeUpload =
            http.Request.ContentType?.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) == true &&
            http.Request.ContentLength > 1024 * 1024;

        tempData["FlashError"] = largeUpload
            ? "The file could not be sent. It may be too large. Please choose a smaller file and try again."
            : "This page was open for a long time, so it couldn't be saved. Please try again.";

        // The check fails before the usual temp-data saving runs, so save the message here.
        tempData.Save();

        // Back to the page the form was on (only addresses on this site), otherwise the start.
        var back = http.Request.Headers.Referer.ToString();
        var url = Uri.TryCreate(back, UriKind.Absolute, out var referer) &&
                  string.Equals(referer.Authority, http.Request.Host.Value, StringComparison.OrdinalIgnoreCase)
            ? referer.PathAndQuery
            : "/";

        context.Result = new LocalRedirectResult(url);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
