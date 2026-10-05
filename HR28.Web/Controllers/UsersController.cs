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
        return Hr28Roles.IsSuperAdministrator(
            HttpContext.Session.GetString("UserRole"));
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

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var created = await _apiClient.PostAsync<UserDto>("Users", model.User, token);

        if (created.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!created.Success || created.Data == null)
        {
            ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(created.Message)
                ? "The user could not be created. Please try again."
                : created.Message);

            return View(model);
        }

        model.GeneratedAuthorizationCode =
            created.Data.AuthorizationCode;

        // The code is shown once; don't let the browser cache this page.
        Response.Headers.CacheControl = "no-store";

        return View(
            "CreateSuccess",
            model);
    }
    /// <summary>Edit a user's details and whether the account is active (same form as Create).</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (!IsSuperAdmin())
            return RedirectToAction("Index", "Dashboard");

        var user = await _apiClient.GetAsync<UserDto>($"Users/{id}", HttpContext.Session.GetString("JwtToken"));

        if (user.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!user.Success || user.Data == null)
        {
            TempData["FlashError"] = "That user could not be found.";
            return RedirectToAction(nameof(Index));
        }

        return View("Create", new UserCreateViewModel
        {
            EditId = id,
            IsActive = user.Data.IsActive,
            User = new CreateUserDto
            {
                NationalId = user.Data.NationalId,
                FullName = user.Data.FullName,
                Address = user.Data.Address,
                MobileNumber = user.Data.MobileNumber,
                Email = user.Data.Email,
                Designation = user.Data.Designation,
                Remarks = user.Data.Remarks
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, UserCreateViewModel model)
    {
        if (!IsSuperAdmin())
            return RedirectToAction("Index", "Dashboard");

        model.EditId = id;

        if (!ModelState.IsValid)
            return View("Create", model);

        var result = await _apiClient.PutAsync<object>(
            $"Users/{id}",
            new
            {
                model.User.NationalId,
                model.User.FullName,
                Address = model.User.Address ?? string.Empty,
                model.User.MobileNumber,
                Email = model.User.Email ?? string.Empty,
                Designation = model.User.Designation ?? string.Empty,
                Remarks = model.User.Remarks ?? string.Empty,
                model.IsActive
            },
            HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(result.Message)
                ? "The changes could not be saved. Please try again."
                : result.Message);

            return View("Create", model);
        }

        TempData["SuccessMessage"] = $"{model.User.FullName} was updated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Permanent delete (the page asks first). Accounts with records must be deactivated instead.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!IsSuperAdmin())
            return RedirectToAction("Index", "Dashboard");

        var result = await _apiClient.DeleteAsync($"Users/{id}", HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
            TempData["SuccessMessage"] = "The user was deleted.";
        else
            TempData["FlashError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "The user could not be deleted."
                : result.Message;

        return RedirectToAction(nameof(Index));
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

        var user = await _apiClient.GetAsync<UserDto>($"Users/{id}", token);

        if (user.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!user.Success || user.Data == null)
        {
            TempData["FlashError"] = "That user could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var roles = await _dashboardService.GetRolesAsync(token) ?? new();

        var extra = await _apiClient.GetAsync<List<string>>($"Permissions/users/{id}", token);

        var model = new UserRolesViewModel
        {
            UserId = id,
            UserName = user.Data.FullName,
            Roles = roles,
            SelectedRoleIds = roles
                .Where(r => user.Data.Roles.Contains(r.Name))
                .Select(r => r.Id)
                .ToList(),
            SelectedPermissions = extra.Data ?? new()
        };

        await LoadPermissionCatalogAsync(model, token);

        return View(model);
    }

    private async Task LoadPermissionCatalogAsync(UserRolesViewModel model, string? token)
    {
        var matrix = await _apiClient.GetAsync<PermissionMatrix>("Permissions", token);

        model.AvailablePermissions = matrix.Data?.Permissions ?? new();
        model.RoleRights = (matrix.Data?.Roles ?? new()).ToDictionary(r => r.RoleId, r => r.Permissions);
        model.AllRightsRoleIds = (matrix.Data?.Roles ?? new()).Where(r => r.HasAllPermissions).Select(r => r.RoleId).ToList();
    }

    /// <summary>Saves the ticked roles. Permissions are the combination of all of them.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(
        UserRolesViewModel model)
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

        if (model.SelectedRoleIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Tick at least one role.");
        }
        else
        {
            var result = await _apiClient.PutAsync<object>(
                $"Users/{model.UserId}/roles",
                new { roleIds = model.SelectedRoleIds },
                token);

            if (result.IsUnauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            if (result.Success)
            {
                // Only roles are assigned here; rights are set per role (Settings → Roles & rights).
                TempData["SuccessMessage"] =
                    $"{model.UserName}'s roles were updated. The change applies straight away.";

                return RedirectToAction(nameof(Index));
            }
            else
            {
                // e.g. "This is the only Administrator..."
                ModelState.AddModelError(string.Empty, result.Message);
            }
        }

        model.Roles =
            await _dashboardService
                .GetRolesAsync(token)
            ?? new();

        await LoadPermissionCatalogAsync(model, token);

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

        var user = await _apiClient.GetAsync<UserDto>($"Users/{id}", token);

        if (user.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!user.Success || user.Data == null)
        {
            TempData["FlashError"] = "That user could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var model = new UserAreasViewModel
        {
            UserId = user.Data.Id,
            UserName = user.Data.FullName,
            Scopes = user.Data.Scopes,
            IsAdministrator = Hr28Roles.IsAdministrator(Hr28Roles.ToSession(user.Data.Roles)),
            Constituencies =
                await _dashboardService
                    .GetConstituenciesAsync(token)
                ?? new()
        };

        return View(model);
    }

    /// <summary>Adds one area (a whole constituency, or one island in it).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignScope(
        Guid userId,
        Guid? constituencyId,
        Guid? islandId)
    {
        if (!IsSuperAdmin())
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }

        if (constituencyId == null)
        {
            TempData["FlashError"] = "Choose a constituency to add.";
            return RedirectToAction(nameof(AssignScope), new { id = userId });
        }

        var result = await _apiClient.PostAsync<UserScopeDto>(
            $"Users/{userId}/scopes",
            new { constituencyId, islandId },
            HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
            TempData["SuccessMessage"] = $"Added {result.Data?.Label}. The change applies straight away.";
        else
            TempData["FlashError"] = result.Message;

        return RedirectToAction(nameof(AssignScope), new { id = userId });
    }

    /// <summary>Removes one area from the user.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveScope(Guid userId, Guid scopeId, string? label)
    {
        if (!IsSuperAdmin())
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }

        var result = await _apiClient.DeleteAsync(
            $"Users/{userId}/scopes/{scopeId}",
            HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
            TempData["SuccessMessage"] = $"Removed {label}. The change applies straight away.";
        else
            TempData["FlashError"] = result.Message;

        return RedirectToAction(nameof(AssignScope), new { id = userId });
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