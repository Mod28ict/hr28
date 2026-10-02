using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

/// <summary>
/// Audit trail. Administrators see every action; other users see their own.
/// The API applies that rule; this page only presents it.
/// </summary>
public class AuditController : AppController
{
    private readonly ApiClient _apiClient;

    public AuditController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index([FromQuery] AuditFilter filter)
    {
        filter.Page = Math.Max(1, filter.Page);
        filter.PageSize = filter.PageSize is 25 or 50 or 100 ? filter.PageSize : 25;

        var query = new List<string>
        {
            $"page={filter.Page}",
            $"pageSize={filter.PageSize}"
        };

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }

        Add("search", filter.Search);
        Add("entityName", filter.EntityName);
        Add("action", filter.ActionName);
        Add("from", filter.From?.ToString("yyyy-MM-dd"));
        Add("to", filter.To?.ToString("yyyy-MM-dd"));

        var result = await _apiClient.GetAsync<PagedResult<AuditEntryDto>>(
            "AuditLogs?" + string.Join("&", query),
            Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        var facets = await _apiClient.GetAsync<AuditFacetsDto>("AuditLogs/facets", Token);

        if (!result.Success)
            TempData["ErrorMessage"] = result.Message;

        return View(new AuditIndexViewModel
        {
            Filter = filter,
            Facets = facets.Data ?? new AuditFacetsDto { IsAdministrator = IsAdministrator },
            Result = result.Data ?? new PagedResult<AuditEntryDto>()
        });
    }
}
