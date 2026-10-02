using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HR28.Web.Filters;

public class SessionAuthorizeAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(
        ActionExecutingContext context)
    {
        var token =
            context.HttpContext.Session
                .GetString("JwtToken");

        if (string.IsNullOrWhiteSpace(token))
        {
            context.HttpContext.Session.Clear();

            context.Result =
                new RedirectToActionResult(
                    "Login",
                    "Auth",
                    null);

            return;
        }

        base.OnActionExecuting(context);
    }
}