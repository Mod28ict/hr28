using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HR28.Web.Filters;

/// <summary>
/// Sensitive pages (Users, uploads, report downloads): opening them asks for a "confirm it's
/// you" code first, unless one was entered in the last few minutes. Only for GET; a form
/// sent after the confirmation ran out is caught by <see cref="StepUpRedirectFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireStepUpAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var http = context.HttpContext;

        if (HttpMethods.IsGet(http.Request.Method) &&
            !string.IsNullOrWhiteSpace(http.Session.GetString("JwtToken")) &&
            !StepUp.IsFresh(http))
        {
            context.Result = new RedirectResult(StepUp.PageFor(StepUp.CurrentPage(http.Request)));
        }
    }
}

/// <summary>
/// When the API refused a request because no recent "confirm it's you" code was entered,
/// show the confirmation page instead of an error, then return to the page the person was on.
/// </summary>
public sealed class StepUpRedirectFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        var http = context.HttpContext;

        if (!http.Items.ContainsKey(StepUp.RequiredItem) || context.Controller is Controllers.StepUpController)
            return;

        http.Session.Remove(StepUp.SessionKey);

        if (!HttpMethods.IsGet(http.Request.Method) && context.Controller is Controller controller)
            controller.TempData["StepUpNote"] = "Your confirmation had run out, so nothing was changed. Confirm it's you, then do it again.";

        context.Result = new RedirectResult(StepUp.PageFor(StepUp.CurrentPage(http.Request)));
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
