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
    [HttpGet]
    public async Task<IActionResult> AssignRole(
        Guid id)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var user =
            (await _dashboardService
                .GetUsersAsync(token))
            ?.FirstOrDefault(x => x.Id == id);

        var model =
            new UserAccessViewModel
            {
                UserId = id,
                UserName =
                    user?.FullName ?? string.Empty,
                Roles =
                    await _dashboardService
                        .GetRolesAsync(token)
                    ?? new()
            };

        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult> AssignRole(
        UserAccessViewModel model)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var success =
            await _dashboardService
                .AssignRoleAsync(
                    new AssignRoleDto
                    {
                        UserId = model.UserId,
                        RoleId = model.RoleId!.Value
                    },
                    token);

        if (!success)
        {
            model.Roles =
                await _dashboardService
                    .GetRolesAsync(token)
                ?? new();

            return View(model);
        }

        return RedirectToAction(
            nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> AssignScope(Guid id)
    {
        var token =
            HttpContext.Session.GetString("JwtToken");

        if (string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(
                "Login",
                "Auth");
        }

        var user =
            (await _dashboardService
                .GetUsersAsync(token))
            ?.FirstOrDefault(x => x.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        var model =
            new UserAccessViewModel
            {
                UserId = user.Id,
                UserName = user.FullName,

                Constituencies =
                    await _dashboardService
                        .GetConstituenciesAsync(token)
                    ?? new(),

                Islands = new()
            };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignScope(
        UserAccessViewModel model)
    {
        var token =
            HttpContext.Session.GetString("JwtToken");

        if (string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(
                "Login",
                "Auth");
        }

        if (!model.ConstituencyId.HasValue)
        {
            ModelState.AddModelError(
                nameof(model.ConstituencyId),
                "Please select a constituency.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateScopeLookupsAsync(
                model,
                token);

            return View(model);
        }

        var success =
            await _dashboardService
                .AssignScopeAsync(
                    new AssignScopeDto
                    {
                        UserId = model.UserId,
                        ConstituencyId =
                            model.ConstituencyId,
                        IslandId =
                            model.IslandId
                    },
                    token);

        if (!success)
        {
            ModelState.AddModelError(
                string.Empty,
                "The scope could not be assigned.");

            await PopulateScopeLookupsAsync(
                model,
                token);

            return View(model);
        }

        TempData["SuccessMessage"] =
            $"Scope assigned successfully to {model.UserName}.";

        return RedirectToAction(
            nameof(Index));
    }

    private async Task PopulateScopeLookupsAsync(
        UserAccessViewModel model,
        string token)
    {
        model.Constituencies =
            await _dashboardService
                .GetConstituenciesAsync(token)
            ?? new();

        model.Islands =
            model.ConstituencyId.HasValue
            && model.ConstituencyId.Value != Guid.Empty
                ? await _dashboardService
                    .GetIslandsByConstituencyAsync(
                        model.ConstituencyId.Value,
                        token)
                    ?? new()
                : new();
    }
    [HttpGet]
    public async Task<IActionResult> GetIslands(
        Guid constituencyId)
    {
        var token =
            HttpContext.Session.GetString("JwtToken");

        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized();
        }

        var islands =
            await _dashboardService
                .GetIslandsByConstituencyAsync(
                    constituencyId,
                    token)
            ?? new();

        return Json(islands);
    }

}