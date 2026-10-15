using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

/// <summary>
/// "Confirm it's you" (owner, 2026-10-15): a fresh SMS code before sensitive actions, even
/// when signed in. One confirmation covers about 15 minutes of work.
/// </summary>
public class StepUpController : AppController
{
    private readonly ApiClient _apiClient;

    public StepUpController(ApiClient apiClient) => _apiClient = apiClient;

    public class SendResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string MaskedMobile { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; }
    }

    public class VerifyResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    [HttpGet]
    public IActionResult Index(string? returnUrl)
    {
        returnUrl = StepUp.SafeReturnUrl(returnUrl);

        if (StepUp.IsFresh(HttpContext))
            return LocalRedirect(returnUrl);

        ViewBag.ReturnUrl = returnUrl;
        ViewBag.CodeSent = TempData.Peek("StepUpSent") as string;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string? returnUrl)
    {
        returnUrl = StepUp.SafeReturnUrl(returnUrl);

        var result = await _apiClient.PostAsync<SendResult>("Auth/step-up/send", new { }, Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (result.Success && result.Data?.Success == true)
            TempData["StepUpSent"] = result.Data.MaskedMobile;
        else
            TempData["StepUpError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "We couldn't send your code right now. Please try again shortly."
                : result.Message;

        return Redirect(StepUp.PageFor(returnUrl));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(string? code, string? returnUrl)
    {
        returnUrl = StepUp.SafeReturnUrl(returnUrl);

        var result = await _apiClient.PostAsync<VerifyResult>(
            "Auth/step-up/verify", new { code = (code ?? string.Empty).Trim() }, Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (!result.Success || string.IsNullOrWhiteSpace(result.Data?.Token))
        {
            TempData["StepUpError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "That code didn't work. Please try again."
                : result.Message;
            TempData.Keep("StepUpSent");
            return Redirect(StepUp.PageFor(returnUrl));
        }

        // The new token carries the confirmation; the API checks its time on every sensitive request.
        HttpContext.Session.SetString("JwtToken", result.Data.Token);
        StepUp.MarkFresh(HttpContext);
        TempData.Remove("StepUpSent");

        return LocalRedirect(returnUrl);
    }
}
