using HR28.Web.Models;
using HR28.Web.Models.Users;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;


namespace HR28.Web.Services;

public class DashboardService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;

    public DashboardService(
        HttpClient httpClient,
        IOptions<ApiSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<DashboardDto?> GetDashboardAsync(
        string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _httpClient.GetAsync(
                $"{_settings.BaseUrl}Dashboard");

        if (response.StatusCode ==
            HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<DashboardDto>();
    }
    public async Task<List<RecentActivityDto>?>
        GetRecentActivityAsync(
            string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient
            .GetFromJsonAsync<List<RecentActivityDto>>(
                $"{_settings.BaseUrl}Dashboard/recent-activity");
    }
    public async Task<List<ConstituencySummaryDto>?>
        GetConstituencySummaryAsync(
            string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            List<ConstituencySummaryDto>>(
                $"{_settings.BaseUrl}Reports/constituency-summary");
    }
    public async Task<List<VoterSearchDto>?>
        SearchVotersAsync(
            string searchTerm,
            string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            List<VoterSearchDto>>(
                $"{_settings.BaseUrl}Voters/search?searchTerm={searchTerm}");
    }
    public async Task<VoterProfileDto?> GetVoterProfileAsync(
        Guid voterId,
        string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<VoterProfileDto>(
            $"{_settings.BaseUrl}Voters/{voterId}/profile");
    }
    public async Task<List<TopInfluencerDto>?>
        GetTopInfluencersAsync(
            string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            List<TopInfluencerDto>>(
                $"{_settings.BaseUrl}Reports/top-influencers");
    }
    public async Task<PledgeStatusSummaryDto?>
        GetPledgeSummaryAsync(
            string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            PledgeStatusSummaryDto>(
                $"{_settings.BaseUrl}Reports/pledge-status-summary");
    }
    public async Task<List<InfluencerDto>?>
        GetInfluencersAsync(string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            List<InfluencerDto>>(
                $"{_settings.BaseUrl}Influencers");
    }
    public async Task<List<VoterSearchDto>?>
        GetRecentVotersAsync(
            string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            List<VoterSearchDto>>(
            $"{_settings.BaseUrl}Voters/recent?count=10");
    }
    public async Task<bool> CreateVoterAsync(
        VoterCreateEditDto request,
        string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _httpClient.PostAsJsonAsync(
                $"{_settings.BaseUrl}Voters",
                request);

        return response.IsSuccessStatusCode;
    }
    public async Task<List<LookupDto>?>
        GetConstituenciesAsync(
            string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            List<LookupDto>>(
            $"{_settings.BaseUrl}Constituencies");
    }
    public async Task<List<LookupDto>?>
        GetIslandsAsync(
            string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            List<LookupDto>>(
            $"{_settings.BaseUrl}Islands");
    }
    public async Task<List<LookupDto>?>
        GetIslandsByConstituencyAsync(
            Guid constituencyId,
            string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            List<LookupDto>>(
            $"{_settings.BaseUrl}Constituencies/{constituencyId}/islands");
    }
    public async Task<bool> CreatePledgeAsync(
        CreatePledgeDto request,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _httpClient.PostAsJsonAsync(
                $"{_settings.BaseUrl}Pledges",
                request);

        return response.IsSuccessStatusCode;
    }
    public async Task<bool> LinkInfluencerAsync(
        LinkInfluencerDto request,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _httpClient.PostAsJsonAsync(
                $"{_settings.BaseUrl}Influencers/link",
                request);

        return response.IsSuccessStatusCode;
    }
    public async Task<bool>
        UpdateRelationshipAsync(
            UpdateInfluencerRelationshipDto request,
            string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _httpClient.PutAsJsonAsync(
                $"{_settings.BaseUrl}Influencers/relationship",
                request);

        return response.IsSuccessStatusCode;
    }
    public async Task<PagedResult<VoterSearchDto>?>
        GetVotersAsync(
            string? token,
            int page = 1,
            int pageSize = 20,
            string? searchTerm = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var url =
            $"{_settings.BaseUrl}Voters" +
            $"?page={page}" +
            $"&pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(
            searchTerm))
        {
            url +=
                $"&searchTerm=" +
                Uri.EscapeDataString(
                    searchTerm.Trim());
        }

        return await _httpClient
            .GetFromJsonAsync<
                PagedResult<VoterSearchDto>>(
                    url);
    }
    public async Task<List<UserDto>?> GetUsersAsync(
        string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            List<UserDto>>(
            $"{_settings.BaseUrl}Users");
    }

    public async Task<UserDto?> CreateUserAsync(
        CreateUserDto request,
        string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _httpClient.PostAsJsonAsync(
                $"{_settings.BaseUrl}Users",
                request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<UserDto>();
    }
    public async Task<List<RoleDto>?> GetRolesAsync(
        string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<
            List<RoleDto>>(
            $"{_settings.BaseUrl}Roles");
    }
    public async Task<bool> AssignRoleAsync(
        AssignRoleDto request,
        string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _httpClient.PostAsJsonAsync(
                $"{_settings.BaseUrl}Users/{request.UserId}/role",
                request);

        return response.IsSuccessStatusCode;
    }
    public async Task<bool> AssignScopeAsync(
        AssignScopeDto request,
        string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _httpClient.PostAsJsonAsync(
                $"{_settings.BaseUrl}Users/{request.UserId}/scope",
                request);

        return response.IsSuccessStatusCode;
    }

}