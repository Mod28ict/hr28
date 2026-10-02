using HR28.Web.Filters;
using HR28.Web.Models;
using HR28.Web.Models.Users;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

[SessionAuthorize]
public class UsersController : Controller
{
    private readonly DashboardService _dashboardService;
    private readonly ApiClient _apiClient;
    private bool IsSuperAdmin()
    {
        return HttpContext.Session.GetString(
            "UserRole")
            == "Super Administrator";
    }

    public UsersController(
        DashboardService dashboardService,
        ApiClient apiClient)
    {
        _dashboardService = dashboardService;
        _apiClient = apiClient;
    }

    /// <summary>
    /// Issues a new authorization code and shows it once. The old code stops
    /// working immediately. The API records this in the audit trail.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetCode(Guid id, string? name)
    {
        if (!IsSuperAdmin())
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }

        var result = await _apiClient.PostAsync<ResetCodeResult>(
            $"Users/{id}/reset-code",
            new { },
            HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!result.Success || string.IsNullOrWhiteSpace(result.Data?.AuthorizationCode))
        {
            TempData["FlashError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "The code could not be reset. Please try again."
                : result.Message;

            return RedirectToAction(nameof(Index));
        }

        ViewBag.IsReset = true;
        ViewBag.UserName = name;

        // The code is shown once; don't let the browser cache this page.
        Response.Headers.CacheControl = "no-store";

        return View(
            "CreateSuccess",
            new UserCreateViewModel
            {
                GeneratedAuthorizationCode = result.Data.AuthorizationCode
            });
    }

    private class ResetCodeResult
    {
        public string AuthorizationCode { get; set; } = string.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsSuperAdmin())
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }
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
        if (!IsSuperAdmin())
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }
        return View(
            new UserCreateViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        UserCreateViewModel model)
    {
        if (!IsSuperAdmin())
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }

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

        // The code is shown once; don't let the browser cache this page.
        Response.Headers.CacheControl = "no-store";

        return View(
            "CreateSuccess",
            model);
    }
    [HttpGet]
    public async Task<IActionResult> AssignRole(
        Guid id)
    {
            if (!IsSuperAdmin())
            {
                return RedirectToAction(
                    "Index",
                    "Dashboard");
            }

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
        if (!IsSuperAdmin())
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        if (model.RoleId == null)
        {
            ModelState.AddModelError(string.Empty, "Please choose a role.");
        }
        else
        {
            var result = await _apiClient.PostAsync<object>(
                $"Users/{model.UserId}/role",
                new AssignRoleDto
                {
                    UserId = model.UserId,
                    RoleId = model.RoleId.Value
                },
                token);

            if (result.IsUnauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            if (result.Success)
            {
                TempData["SuccessMessage"] =
                    $"{model.UserName}'s role was updated. The change applies straight away.";

                return RedirectToAction(nameof(Index));
            }

            // e.g. "This is the only Administrator..."
            ModelState.AddModelError(string.Empty, result.Message);
        }

        model.Roles =
            await _dashboardService
                .GetRolesAsync(token)
            ?? new();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> AssignScope(Guid id)
    {
        if (!IsSuperAdmin())
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }
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
        if (!IsSuperAdmin())
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }
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