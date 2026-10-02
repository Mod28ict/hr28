using System.Net;
using HR28.Web.Models;
using HR28.Web.Models.Users;

namespace HR28.Web.Services;

/// <summary>
/// API calls used by the older pages. Every call goes through <see cref="ApiClient"/>
/// (visitor IP forwarded, safe messages). Failures the page cannot handle itself
/// (session ended, no permission, too many requests, service down) throw
/// <see cref="ApiCallException"/>, which <see cref="Filters.ApiFailureExceptionFilter"/>
/// turns into a sign-in redirect or a friendly page.
/// </summary>
public class DashboardService
{
    private readonly ApiClient _api;

    public DashboardService(ApiClient api)
    {
        _api = api;
    }

    /// <summary>
    /// Plain-language reason the last save returned false (e.g. a duplicate),
    /// suitable for showing on the form.
    /// </summary>
    public string LastErrorMessage { get; private set; } = string.Empty;

    public Task<DashboardDto?> GetDashboardAsync(string? token) =>
        GetAsync<DashboardDto>("Dashboard", token);

    public Task<List<VoterSearchDto>?> SearchVotersAsync(string searchTerm, string? token) =>
        GetAsync<List<VoterSearchDto>>(
            "Voters/search?searchTerm=" + Uri.EscapeDataString(searchTerm.Trim()), token);

    /// <summary>Returns null when the voter does not exist or is outside the user's areas.</summary>
    public Task<VoterProfileDto?> GetVoterProfileAsync(Guid voterId, string? token) =>
        GetAsync<VoterProfileDto>($"Voters/{voterId}/profile", token);

    public Task<List<InfluencerDto>?> GetInfluencersAsync(string? token) =>
        GetAsync<List<InfluencerDto>>("Influencers", token);

    public Task<bool> UpdateVoterAsync(Guid id, VoterCreateEditDto request, string? token) =>
        SaveAsync(_api.PutAsync<object>($"Voters/{id}", request, token));

    public Task<List<LookupDto>?> GetConstituenciesAsync(string? token) =>
        GetAsync<List<LookupDto>>("Constituencies", token);

    public Task<List<LookupDto>?> GetIslandsAsync(string? token) =>
        GetAsync<List<LookupDto>>("Islands", token);

    public Task<List<LookupDto>?> GetIslandsByConstituencyAsync(Guid constituencyId, string? token) =>
        GetAsync<List<LookupDto>>($"Constituencies/{constituencyId}/islands", token);

    public Task<bool> CreateEncounterAsync(CreateEncounterDto request, string? token) =>
        SaveAsync(_api.PostAsync<object>("Encounters", request, token));

    public Task<bool> CreatePledgeAsync(CreatePledgeDto request, string? token) =>
        SaveAsync(_api.PostAsync<object>("Pledges", request, token));

    public Task<bool> LinkInfluencerAsync(LinkInfluencerDto request, string? token) =>
        SaveAsync(_api.PostAsync<object>("Influencers/link", request, token));

    public Task<bool> UpdateRelationshipAsync(UpdateInfluencerRelationshipDto request, string? token) =>
        SaveAsync(_api.PutAsync<object>("Influencers/relationship", request, token));

    public Task<PagedResult<VoterSearchDto>?> GetVotersAsync(
        string? token,
        int page = 1,
        int pageSize = 20,
        string? searchTerm = null)
    {
        var url = $"Voters?page={page}&pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(searchTerm))
            url += "&searchTerm=" + Uri.EscapeDataString(searchTerm.Trim());

        return GetAsync<PagedResult<VoterSearchDto>>(url, token);
    }

    public Task<List<UserDto>?> GetUsersAsync(string? token) =>
        GetAsync<List<UserDto>>("Users", token);

    /// <summary>Returns null (with <see cref="LastErrorMessage"/>) when the user could not be created.</summary>
    public async Task<UserDto?> CreateUserAsync(CreateUserDto request, string? token)
    {
        var result = await _api.PostAsync<UserDto>("Users", request, token);

        return await SaveAsync(Task.FromResult(result)) ? result.Data : null;
    }

    public Task<List<RoleDto>?> GetRolesAsync(string? token) =>
        GetAsync<List<RoleDto>>("Roles", token);

    private async Task<T?> GetAsync<T>(string path, string? token)
    {
        var result = await _api.GetAsync<T>(path, token);

        if (result.Success)
            return result.Data;

        if (result.StatusCode == HttpStatusCode.NotFound)
            return default;

        throw new ApiCallException(result.StatusCode, result.Message);
    }

    /// <summary>
    /// True when saved. Validation and business-rule refusals (400, 404, 409)
    /// return false so the form can show <see cref="LastErrorMessage"/>; everything
    /// else throws so the user is signed out or sees a friendly page.
    /// </summary>
    private async Task<bool> SaveAsync<T>(Task<ApiResult<T>> call)
    {
        var result = await call;

        if (result.Success)
        {
            LastErrorMessage = string.Empty;
            return true;
        }

        if (result.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.NotFound
            or HttpStatusCode.Conflict
            or HttpStatusCode.UnprocessableEntity)
        {
            LastErrorMessage = result.Message;
            return false;
        }

        throw new ApiCallException(result.StatusCode, result.Message);
    }
}
