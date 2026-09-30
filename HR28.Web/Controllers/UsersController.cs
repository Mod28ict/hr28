using HR28.Web.Models.Users;
using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

public class UsersController : Controller
{
    private readonly DashboardService _dashboardService;

    public UsersController(
        DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var users =
            await _dashboardService
                .GetUsersAsync(token);

        return View(users ?? new List<UserDto>());
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(
            new UserCreateViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        UserCreateViewModel model)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var createdUser =
            await _dashboardService
                .CreateUserAsync(
                    model.User,
                    token);

        if (createdUser == null)
        {
            return View(model);
        }

        model.GeneratedAuthorizationCode =
            createdUser.AuthorizationCode;

        return View(
            "CreateSuccess",
            model);
    }
}