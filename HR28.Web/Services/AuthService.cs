using HR28.Web.Models;
using HR28.Web.Models.Auth;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace HR28.Web.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;

    public AuthService(
        HttpClient httpClient,
        IOptions<ApiSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    /// <summary>
    /// Requests an OTP. Returns how many seconds the code is valid,
    /// or null if the request was rejected.
    /// </summary>
    public async Task<int?> GenerateOtpAsync(
        GenerateOtpRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"{_settings.BaseUrl}Auth/generate-otp",
            request);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content
            .ReadFromJsonAsync<GenerateOtpResult>();

        return result?.ExpiresInSeconds > 0
            ? result.ExpiresInSeconds
            : 300;
    }

    private class GenerateOtpResult
    {
        public int ExpiresInSeconds { get; set; }
    }

    public async Task<VerifyOtpResponse?>
        VerifyOtpAsync(
            VerifyOtpRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"{_settings.BaseUrl}Auth/verify-otp",
            request);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content
            .ReadFromJsonAsync<VerifyOtpResponse>();
    }
}