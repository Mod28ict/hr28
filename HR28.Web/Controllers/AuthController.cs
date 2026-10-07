using HR28.Web.Models.Auth;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

public class AuthController : Controller
{
    /// <summary>
    /// "Remember me on this device": holds the device key the API issued. HttpOnly (page
    /// scripts can't read it), Secure, SameSite=Strict. The authorization code itself is
    /// never stored in the browser, and the SMS code is still needed on every sign-in.
    /// </summary>
    public const string DeviceCookie = "HR28.Device";

    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    private string? DeviceToken => Request.Cookies[DeviceCookie];

    private void ForgetDeviceCookie() =>
        Response.Cookies.Delete(DeviceCookie, new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        await PrepareLoginAsync();
        return View();
    }

    /// <summary>
    /// The sign-in page: the "Remember me" choice (when turned on in Settings → System), and
    /// on a remembered device a "Welcome back" panel instead of the code box.
    /// </summary>
    private async Task PrepareLoginAsync()
    {
        var days = await _authService.GetRememberDeviceDaysAsync();
        ViewBag.RememberDeviceDays = days;

        if (TempData["LoginError"] is string error)
            ViewBag.Error = error;

        var token = DeviceToken;

        if (string.IsNullOrEmpty(token))
            return;

        if (days <= 0)
        {
            ForgetDeviceCookie();
            return;
        }

        var (device, unreachable) = await _authService.GetRememberedDeviceAsync(token);

        if (device != null)
        {
            ViewBag.RememberedDevice = device;
        }
        else if (!unreachable)
        {
            ForgetDeviceCookie();
            ViewBag.Error ??= "This device is no longer remembered. Please enter your authorization code.";
        }
    }

    /// <summary>
    /// An old bookmark or link to sign-out lands safely on the dashboard instead
    /// of an error page; it does not sign anyone out.
    /// </summary>
    [HttpGet]
    [ActionName("Logout")]
    public IActionResult LogoutLink() => RedirectToAction("Index", "Dashboard");

    /// <summary>
    /// POST only (anti-forgery checked site-wide). Clears the session, which holds
    /// the API token, and removes the session cookie from the browser. A remembered
    /// device stays remembered (that is its purpose); "Not you?" on the sign-in page forgets it.
    /// </summary>
    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        Response.Cookies.Delete("HR28.Session");

        return RedirectToAction(
            "Login",
            "Auth");
    }

    /// <summary>"Not you? Use an authorization code": forgets this device (server and browser).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgetDevice()
    {
        if (DeviceToken is { Length: > 0 } token)
            await _authService.ForgetDeviceAsync(token);

        ForgetDeviceCookie();

        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        GenerateOtpRequest request,
        bool useDevice = false)
    {
        if (useDevice)
        {
            if (string.IsNullOrEmpty(DeviceToken))
                return RedirectToAction(nameof(Login));

            request.AuthorizationCode = string.Empty;
            request.DeviceToken = DeviceToken;
            request.RememberDevice = false;
        }

        var result =
            await _authService.GenerateOtpAsync(request);

        if (!result.Success)
        {
            if (result.DeviceNotRecognised)
                ForgetDeviceCookie();

            if (useDevice)
            {
                // Back to the sign-in page, which shows the right panel for this device.
                TempData["LoginError"] = result.Message;
                return RedirectToAction(nameof(Login));
            }

            // Plain-language reason from the API, e.g. "Please wait 45 seconds..."
            ViewBag.Error = result.Message;
            ViewBag.RememberDeviceDays = await _authService.GetRememberDeviceDaysAsync();

            return View(request);
        }

        // Every sign-in starts clean: nothing carried over from an earlier attempt.
        ClearSignInState();

        if (useDevice)
            TempData["UseDevice"] = "1";
        else
            TempData["AuthorizationCode"] = request.AuthorizationCode;

        if (request.RememberDevice)
            TempData["RememberDevice"] = "1";

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
        if (request.UseDevice)
        {
            if (string.IsNullOrEmpty(DeviceToken))
                return RedirectToAction(nameof(Login));

            request.AuthorizationCode = string.Empty;
            request.DeviceToken = DeviceToken;
            request.RememberDevice = false;
        }
        else if (request.RememberDevice)
        {
            request.DeviceName = DeviceLabel(Request.Headers.UserAgent.ToString());
        }

        var response =
            await _authService.VerifyOtpAsync(request);

        if (response?.DeviceNotRecognised == true)
        {
            ForgetDeviceCookie();
            TempData["LoginError"] = response.Message;
            return RedirectToAction(nameof(Login));
        }

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

        // "Remember me": the device key goes only into an HttpOnly cookie.
        if (!string.IsNullOrEmpty(response.DeviceToken) && response.DeviceExpiresAt is { } expires)
        {
            Response.Cookies.Append(DeviceCookie, response.DeviceToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                Expires = new DateTimeOffset(DateTime.SpecifyKind(expires, DateTimeKind.Utc))
            });
        }

        ClearSignInState();

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

    /// <summary>Forgets what the code page kept between attempts (code, device flags, countdown).</summary>
    private void ClearSignInState()
    {
        TempData.Remove("AuthorizationCode");
        TempData.Remove("UseDevice");
        TempData.Remove("RememberDevice");
        TempData.Remove("OtpExpiresAtMs");
    }

    /// <summary>A readable device label for the user's list of remembered devices, e.g. "Chrome on Windows".</summary>
    public static string DeviceLabel(string userAgent)
    {
        var ua = userAgent ?? string.Empty;

        bool Has(string text) => ua.Contains(text, StringComparison.OrdinalIgnoreCase);

        var browser =
            Has("Edg/") ? "Edge" :
            Has("OPR/") || Has("Opera") ? "Opera" :
            Has("Firefox/") ? "Firefox" :
            Has("SamsungBrowser") ? "Samsung Internet" :
            Has("Chrome/") ? "Chrome" :
            Has("Safari/") ? "Safari" :
            "A web browser";

        var system =
            Has("Windows") ? "Windows" :
            Has("Android") ? "Android" :
            Has("iPhone") ? "iPhone" :
            Has("iPad") ? "iPad" :
            Has("Mac OS") ? "Mac" :
            Has("Linux") ? "Linux" :
            null;

        return system == null ? browser : $"{browser} on {system}";
    }
}
