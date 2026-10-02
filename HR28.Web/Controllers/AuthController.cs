using HR28.Web.Models.Auth;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

public class AuthController : Controller
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        return RedirectToAction(
            "Login",
            "Auth");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        GenerateOtpRequest request)
    {
        var result =
            await _authService.GenerateOtpAsync(request);

        if (!result.Success)
        {
            // Plain-language reason from the API, e.g. "Please wait 45 seconds..."
            ViewBag.Error = result.Message;

            return View();
        }

        TempData["AuthorizationCode"] =
            request.AuthorizationCode;

        // Absolute expiry so the countdown stays correct if the page is re-shown after a wrong code.
        TempData["OtpExpiresAtMs"] =
            DateTimeOffset.UtcNow
                .AddSeconds(result.ExpiresInSeconds)
                .ToUnixTimeMilliseconds()
                .ToString();

        return RedirectToAction(
            "VerifyOtp");
    }

    [HttpGet]
    public IActionResult VerifyOtp()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(
        VerifyOtpRequest request)
    {
        var response =
            await _authService.VerifyOtpAsync(request);

        if (response == null ||
            !response.Success)
        {
            ViewBag.Error =
                string.IsNullOrWhiteSpace(response?.Message)
                    ? "That code didn't work. Please try again."
                    : response.Message;

            // Keep the authorization code so the user can retry the same code.
            return View(request);
        }

        HttpContext.Session.SetString(
            "JwtToken",
            response.Token);
        // All roles, so menus reflect the combination of the user's permissions.
        HttpContext.Session.SetString(
            "UserRole",
            Hr28Roles.ToSession(
                response.Roles.Count > 0
                    ? response.Roles
                    : new[] { response.RoleName }));

        HttpContext.Session.SetString(
            "UserName",
            response.FullName);

        HttpContext.Session.SetString(
            "UserId",
            response.UserId.ToString());

        return RedirectToAction(
            "Index",
            "Dashboard");
    }
}