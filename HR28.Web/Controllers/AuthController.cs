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

    [HttpPost]
    public async Task<IActionResult> Login(
        GenerateOtpRequest request)
    {
        var success =
            await _authService.GenerateOtpAsync(request);

        if (!success)
        {
            ViewBag.Error =
                "Failed to generate OTP.";

            return View();
        }

        TempData["AuthorizationCode"] =
            request.AuthorizationCode;

        return RedirectToAction(
            "VerifyOtp");
    }

    [HttpGet]
    public IActionResult VerifyOtp()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> VerifyOtp(
        VerifyOtpRequest request)
    {
        var response =
            await _authService.VerifyOtpAsync(request);

        if (response == null ||
            !response.Success)
        {
            ViewBag.Error =
                "OTP verification failed.";

            return View();
        }

        HttpContext.Session.SetString(
            "JwtToken",
            response.Token);
        HttpContext.Session.SetString(
            "UserRole",
            response.RoleName);

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