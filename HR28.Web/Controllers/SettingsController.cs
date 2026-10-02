using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

/// <summary>
/// Settings: appearance and my account for everyone; system settings and
/// constituencies/islands for administrators (the API enforces this too).
/// </summary>
public class SettingsController : AppController
{
    private static readonly string[] Tabs = { "appearance", "account", "system", "geography", "permissions" };
    private static readonly string[] AdminTabs = { "system", "geography" };
    private static readonly string[] SuperAdminTabs = { "permissions" };

    private bool IsSuperAdministrator => Hr28Roles.IsSuperAdministrator(Role);

    private readonly ApiClient _apiClient;

    public SettingsController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index(
        string? tab,
        Guid? constituency,
        Guid? editConstituency,
        Guid? editIsland,
        bool newConstituency = false,
        bool newIsland = false)
    {
        tab = Tabs.Contains(tab) ? tab! : "appearance";

        if (AdminTabs.Contains(tab) && !IsAdministrator)
            tab = "appearance";

        if (SuperAdminTabs.Contains(tab) && !IsSuperAdministrator)
            tab = "appearance";

        var model = new SettingsViewModel
        {
            Tab = tab,
            IsAdministrator = IsAdministrator,
            IsSuperAdministrator = IsSuperAdministrator,
            IslandConstituencyFilter = constituency
        };

        if (tab == "permissions")
        {
            var matrix = await _apiClient.GetAsync<PermissionMatrix>("Permissions", Token);

            if (HandleApiFailure(matrix) is { } redirect)
                return redirect;

            model.Permissions = matrix.Data;
        }

        if (tab == "account")
        {
            var account = await _apiClient.GetAsync<MyAccountDto>("Settings/me", Token);

            if (HandleApiFailure(account) is { } redirect)
                return redirect;

            model.Account = account.Data;
        }

        if (tab == "system")
        {
            var system = await _apiClient.GetAsync<SystemSettingsDto>("Settings/system", Token);

            if (HandleApiFailure(system) is { } redirect)
                return redirect;

            model.System = system.Data;
        }

        if (tab == "geography")
        {
            var constituencies = await _apiClient.GetAsync<List<ConstituencyAdminDto>>("Constituencies", Token);

            if (HandleApiFailure(constituencies) is { } redirect)
                return redirect;

            var islands = await _apiClient.GetAsync<List<IslandAdminDto>>("Islands", Token);

            model.Constituencies = (constituencies.Data ?? new())
                .OrderBy(c => c.Name)
                .ToList();

            model.Islands = (islands.Data ?? new())
                .Where(i => constituency == null || i.ConstituencyId == constituency)
                .OrderBy(i => i.ConstituencyName)
                .ThenBy(i => i.Name)
                .ToList();

            if (newConstituency)
            {
                model.ConstituencyForm = new ConstituencyForm();
            }
            else if (editConstituency.HasValue &&
                     model.Constituencies.FirstOrDefault(c => c.Id == editConstituency) is { } c)
            {
                model.ConstituencyForm = new ConstituencyForm { Id = c.Id, Code = c.Code, Name = c.Name };
            }

            if (newIsland)
            {
                model.IslandForm = new IslandForm { ConstituencyId = constituency };
            }
            else if (editIsland.HasValue &&
                     (islands.Data ?? new()).FirstOrDefault(i => i.Id == editIsland) is { } i)
            {
                model.IslandForm = new IslandForm
                {
                    Id = i.Id,
                    Name = i.Name,
                    Atoll = i.Atoll,
                    ConstituencyId = i.ConstituencyId
                };
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSystem(SystemSettingsDto settings)
    {
        if (!IsAdministrator)
            return RedirectToAction(nameof(Index));

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = FirstError();
            return RedirectToAction(nameof(Index), new { tab = "system" });
        }

        var result = await _apiClient.PutAsync<SystemSettingsDto>("Settings/system", settings, Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (result.Success)
        {
            HttpContext.Session.SetString("CampaignName", result.Data?.CampaignName ?? settings.CampaignName);
            TempData["SuccessMessage"] = "System settings saved.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "system" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveConstituency(ConstituencyForm form)
    {
        if (!IsAdministrator)
            return RedirectToAction(nameof(Index));

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = FirstError();
            return RedirectToAction(nameof(Index), new { tab = "geography", newConstituency = form.Id == null, editConstituency = form.Id });
        }

        var body = new { code = form.Code, name = form.Name };

        var result = form.Id.HasValue
            ? await _apiClient.PutAsync<object>($"Constituencies/{form.Id}", body, Token)
            : await _apiClient.PostAsync<object>("Constituencies", body, Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index), new { tab = "geography", newConstituency = form.Id == null, editConstituency = form.Id });
        }

        TempData["SuccessMessage"] = form.Id.HasValue
            ? $"{form.Name} was updated."
            : $"{form.Name} was added.";

        return RedirectToAction(nameof(Index), new { tab = "geography" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIsland(IslandForm form)
    {
        if (!IsAdministrator)
            return RedirectToAction(nameof(Index));

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = FirstError();
            return RedirectToAction(nameof(Index), new { tab = "geography", newIsland = form.Id == null, editIsland = form.Id, constituency = form.ConstituencyId });
        }

        var body = new { name = form.Name, atoll = form.Atoll, constituencyId = form.ConstituencyId };

        var result = form.Id.HasValue
            ? await _apiClient.PutAsync<object>($"Islands/{form.Id}", body, Token)
            : await _apiClient.PostAsync<object>("Islands", body, Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index), new { tab = "geography", newIsland = form.Id == null, editIsland = form.Id, constituency = form.ConstituencyId });
        }

        TempData["SuccessMessage"] = form.Id.HasValue
            ? $"{form.Name} was updated."
            : $"{form.Name} was added.";

        return RedirectToAction(nameof(Index), new { tab = "geography", constituency = form.ConstituencyId });
    }

    /// <summary>
    /// Saves the roles × rights grid. Each ticked box posts "roleId:permission".
    /// Only roles whose ticks changed are sent to the API.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePermissions(List<string>? grants)
    {
        if (!IsSuperAdministrator)
            return RedirectToAction(nameof(Index));

        var current = await _apiClient.GetAsync<PermissionMatrix>("Permissions", Token);

        if (HandleApiFailure(current) is { } redirect)
            return redirect;

        if (!current.Success || current.Data == null)
        {
            TempData["ErrorMessage"] = current.Message;
            return RedirectToAction(nameof(Index), new { tab = "permissions" });
        }

        var ticked = (grants ?? new())
            .Select(g => g.Split(':', 2))
            .Where(p => p.Length == 2 && Guid.TryParse(p[0], out _))
            .GroupBy(p => Guid.Parse(p[0]))
            .ToDictionary(g => g.Key, g => g.Select(p => p[1]).ToHashSet());

        var changedRoles = 0;

        foreach (var role in current.Data.Roles.Where(r => !r.HasAllPermissions))
        {
            var wanted = ticked.TryGetValue(role.RoleId, out var set) ? set : new HashSet<string>();

            if (wanted.SetEquals(role.Permissions))
                continue;

            var result = await _apiClient.PutAsync<object>(
                $"Permissions/roles/{role.RoleId}",
                new { permissions = wanted.ToList() },
                Token);

            if (HandleApiFailure(result) is { } failed)
                return failed;

            if (!result.Success)
            {
                TempData["ErrorMessage"] = $"{Hr28Roles.DisplayName(role.RoleName)}: {result.Message}";
                return RedirectToAction(nameof(Index), new { tab = "permissions" });
            }

            changedRoles++;
        }

        TempData["SuccessMessage"] = changedRoles == 0
            ? "No changes to save."
            : "Permissions saved. They apply straight away.";

        return RedirectToAction(nameof(Index), new { tab = "permissions" });
    }

    private string FirstError() =>
        ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
        ?? "Please check the form and try again.";
}
