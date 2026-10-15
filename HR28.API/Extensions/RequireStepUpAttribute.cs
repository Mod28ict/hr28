using HR28.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HR28.API.Extensions;

/// <summary>
/// Sensitive action (owner, 2026-10-15): needs a "confirm it's you" code entered within the
/// last <see cref="StepUpRules.ValidFor"/>. Runs after the role and right checks, so people
/// without access still get a plain 403. Refusals carry <see cref="StepUpRules.RequiredHeader"/>
/// so the web app can ask the person to confirm.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireStepUpAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return;

        if (long.TryParse(user.FindFirst(StepUpRules.Claim)?.Value, out var unix) &&
            DateTime.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime < StepUpRules.ValidFor)
            return;

        context.HttpContext.Response.Headers[StepUpRules.RequiredHeader] = "1";
        context.Result = new ObjectResult(new
        {
            message = "For your security, please confirm it's you with a code sent by SMS.",
            stepUpRequired = true
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }
}
