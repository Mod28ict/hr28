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
    private static readonly string[] Tabs = { "appearance", "account", "system", "geography", "lists", "permissions" };
    private static readonly string[] AdminTabs = { "system", "geography", "lists" };
    private static readonly string[] SuperAdminTabs = { "permissions" };

    private bool IsSuperAdministrator => Hr28Roles.IsSuperAdministrator(Role);

    private readonly ApiClient _apiClient;
    private readonly BrandingService _branding;

    public SettingsController(ApiClient apiClient, BrandingService branding)
    {
        _apiClient = apiClient;
        _branding = branding;
    }

    public async Task<IActionResult> Index(
        string? tab,
        Guid? constituency,
        Guid? editConstituency,
        Guid? editIsland,
        Guid? editCategory,
        Guid? editParty,
        Guid? role,
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

            // The role being edited: the one asked for, or the first in the list.
            model.SelectedRoleId = matrix.Data?.Roles.Any(r => r.RoleId == role) == true
                ? role
                : matrix.Data?.Roles.FirstOrDefault()?.RoleId;
        }

        if (tab == "lists")
        {
            var categories = await _apiClient.GetAsync<List<InfluencerCategoryDto>>("InfluencerCategories", Token);

            if (HandleApiFailure(categories) is { } redirect)
                return redirect;

            model.InfluencerCategories = categories.Data ?? new();
            model.EditCategoryId = editCategory;

            var parties = await _apiClient.GetAsync<List<PoliticalPartyDto>>("PoliticalParties", Token);
            model.Parties = parties.Data ?? new();
            model.EditPartyId = editParty;
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
    [RequestSizeLimit(20 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile? logo)
    {
        if (!IsAdministrator)
            return RedirectToAction(nameof(Index));

        if (logo == null || logo.Length == 0)
            TempData["ErrorMessage"] = "Please choose a logo first.";
        else if (logo.Length > 1024 * 1024)
            TempData["ErrorMessage"] = $"The logo is too large ({logo.Length / (1024.0 * 1024.0):0.#} MB). Please choose one under 1 MB.";
        else
        {
            var result = await _apiClient.PostFileAsync<object>("Settings/logo", logo, Token);

            if (HandleApiFailure(result) is { } redirect)
                return redirect;

            if (result.Success)
            {
                _branding.Forget();
                TempData["SuccessMessage"] = "Logo saved.";
            }
            else
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(result.Message) ? "The logo could not be saved." : result.Message;
            }
        }

        return RedirectToAction(nameof(Index), new { tab = "system" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLogo()
    {
        if (!IsAdministrator)
            return RedirectToAction(nameof(Index));

        var result = await _apiClient.DeleteAsync("Settings/logo", Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        _branding.Forget();
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success ? "Logo removed." : "The logo could not be removed.";

        return RedirectToAction(nameof(Index), new { tab = "system" });
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
            _branding.Forget();
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

    /// <summary>Adds an influencer category (no id) or renames one.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInfluencerCategory(Guid? id, string? name)
    {
        if (!IsAdministrator)
            return RedirectToAction(nameof(Index));

        var body = new { name = name?.Trim() ?? string.Empty };

        var result = id.HasValue
            ? await _apiClient.PutAsync<InfluencerCategoryDto>($"InfluencerCategories/{id}", body, Token)
            : await _apiClient.PostAsync<InfluencerCategoryDto>("InfluencerCategories", body, Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index), new { tab = "lists", editCategory = id });
        }

        TempData["SuccessMessage"] = id.HasValue
            ? $"Category renamed to {result.Data?.Name ?? body.name}."
            : $"Category {result.Data?.Name ?? body.name} was added.";

        return RedirectToAction(nameof(Index), new { tab = "lists" });
    }

    /// <summary>Deletes an influencer category that no influencer uses.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInfluencerCategory(Guid id)
    {
        if (!IsAdministrator)
            return RedirectToAction(nameof(Index));

        var result = await _apiClient.DeleteAsync($"InfluencerCategories/{id}", Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (result.Success)
            TempData["SuccessMessage"] = "The category was deleted.";
        else
            TempData["ErrorMessage"] = result.Message;

        return RedirectToAction(nameof(Index), new { tab = "lists" });
    }

    /// <summary>Adds a political party (no id) or changes its name / short name.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveParty(Guid? id, string? name, string? shortName)
    {
        if (!IsAdministrator)
            return RedirectToAction(nameof(Index));

        var body = new { name = name?.Trim() ?? string.Empty, shortName = shortName?.Trim() ?? string.Empty };

        var result = id.HasValue
            ? await _apiClient.PutAsync<PoliticalPartyDto>($"PoliticalParties/{id}", body, Token)
            : await _apiClient.PostAsync<PoliticalPartyDto>("PoliticalParties", body, Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index), new { tab = "lists", editParty = id });
        }

        TempData["SuccessMessage"] = id.HasValue
            ? $"{result.Data?.Name ?? body.name} was updated."
            : $"{result.Data?.Name ?? body.name} was added.";

        return RedirectToAction(nameof(Index), new { tab = "lists" });
    }

    /// <summary>Deletes a party that no voter has.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteParty(Guid id)
    {
        if (!IsAdministrator)
            return RedirectToAction(nameof(Index));

        var result = await _apiClient.DeleteAsync($"PoliticalParties/{id}", Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (result.Success)
            TempData["SuccessMessage"] = "The party was deleted.";
        else
            TempData["ErrorMessage"] = result.Message;

        return RedirectToAction(nameof(Index), new { tab = "lists" });
    }

    /// <summary>Chooses the party the Voters list opens on (no id = all parties).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultParty(Guid? id)
    {
        if (!IsAdministrator)
            return RedirectToAction(nameof(Index));

        var result = await _apiClient.PutAsync<object>(
            "PoliticalParties/default-filter" + (id.HasValue ? $"?id={id}" : string.Empty),
            new { },
            Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (result.Success)
            TempData["SuccessMessage"] = "Saved. The Voters list now opens on the party you chose.";
        else
            TempData["ErrorMessage"] = result.Message;

        return RedirectToAction(nameof(Index), new { tab = "lists" });
    }

    /// <summary>A new custom role; it has no rights until they are ticked.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRole(string? name, string? description, string? voterProfileView, string? startPage)
    {
        if (!IsSuperAdministrator)
            return RedirectToAction(nameof(Index));

        var result = await _apiClient.PostAsync<CreatedRole>(
            "Permissions/roles",
            new
            {
                name = name?.Trim() ?? string.Empty,
                description = description?.Trim() ?? string.Empty,
                voterProfileView = voterProfileView ?? "Full",
                startPage = startPage ?? "Dashboard"
            },
            Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index), new { tab = "permissions" });
        }

        TempData["SuccessMessage"] = $"The role \"{name?.Trim()}\" was created. Now tick what it may do and save.";

        return RedirectToAction(nameof(Index), new { tab = "permissions", role = result.Data.Id });
    }

    private class CreatedRole
    {
        public Guid Id { get; set; }
    }

    /// <summary>
    /// Saves one role: name and description (custom roles), what opens when a voter is
    /// opened, and its rights. Only changed parts are sent; every change is audited.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRole(
        Guid roleId,
        string? name,
        string? description,
        string? voterProfileView,
        string? startPage,
        List<string>? grants)
    {
        if (!IsSuperAdministrator)
            return RedirectToAction(nameof(Index));

        var current = await _apiClient.GetAsync<PermissionMatrix>("Permissions", Token);

        if (HandleApiFailure(current) is { } redirect)
            return redirect;

        var role = current.Data?.Roles.FirstOrDefault(r => r.RoleId == roleId);

        if (role == null)
        {
            TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(current.Message) ? "That role no longer exists." : current.Message;
            return RedirectToAction(nameof(Index), new { tab = "permissions" });
        }

        var details = await _apiClient.PutAsync<object>(
            $"Permissions/roles/{roleId}/details",
            new
            {
                name = role.IsBuiltIn ? role.RoleName : name?.Trim() ?? string.Empty,
                description = role.IsBuiltIn ? role.Description : description?.Trim() ?? string.Empty,
                voterProfileView = voterProfileView ?? role.VoterProfileView,
                startPage = startPage ?? role.StartPage
            },
            Token);

        if (HandleApiFailure(details) is { } failed)
            return failed;

        if (!details.Success)
        {
            TempData["ErrorMessage"] = details.Message;
            return RedirectToAction(nameof(Index), new { tab = "permissions", role = roleId });
        }

        var wanted = (grants ?? new()).ToHashSet();

        if (!role.HasAllPermissions && !wanted.SetEquals(role.Permissions))
        {
            var rights = await _apiClient.PutAsync<object>(
                $"Permissions/roles/{roleId}",
                new { permissions = wanted.ToList() },
                Token);

            if (HandleApiFailure(rights) is { } rightsFailed)
                return rightsFailed;

            if (!rights.Success)
            {
                TempData["ErrorMessage"] = rights.Message;
                return RedirectToAction(nameof(Index), new { tab = "permissions", role = roleId });
            }
        }

        TempData["SuccessMessage"] = "Saved. The changes apply straight away.";

        return RedirectToAction(nameof(Index), new { tab = "permissions", role = roleId });
    }

    /// <summary>Deletes a custom role nobody has.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRole(Guid roleId)
    {
        if (!IsSuperAdministrator)
            return RedirectToAction(nameof(Index));

        var result = await _apiClient.DeleteAsync($"Permissions/roles/{roleId}", Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (result.Success)
        {
            TempData["SuccessMessage"] = "The role was deleted.";
            return RedirectToAction(nameof(Index), new { tab = "permissions" });
        }

        TempData["ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Index), new { tab = "permissions", role = roleId });
    }

    private string FirstError() =>
        ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
        ?? "Please check the form and try again.";
}
