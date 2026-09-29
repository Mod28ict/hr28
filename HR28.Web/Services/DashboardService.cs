using HR28.Web.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Net.Http.Headers;

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

    public async Task<DashboardDto?>
        GetDashboardAsync(string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        return await _httpClient.GetFromJsonAsync<DashboardDto>(
            $"{_settings.BaseUrl}Dashboard");
    }
}