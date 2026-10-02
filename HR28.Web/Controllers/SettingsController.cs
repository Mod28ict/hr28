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
    private static readonly string[] Tabs = { "appearance", "account", "system", "geography" };
    private static readonly string[] AdminTabs = { "system", "geography" };

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

        var model = new SettingsViewModel
        {
            Tab = tab,
            IsAdministrator = IsAdministrator,
            IslandConstituencyFilter = constituency
        };

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

    private string FirstError() =>
        ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
        ?? "Please check the form and try again.";
}
