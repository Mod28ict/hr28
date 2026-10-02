using HR28.Web.Models;
using HR28.Web.Models.Auth;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HR28.Web.Services;

/// <summary>Result of asking the API to send a sign-in code.</summary>
public class OtpRequestResult
{
    public bool Success { get; init; }

    /// <summary>How long the code is valid (when Success).</summary>
    public int ExpiresInSeconds { get; init; }

    /// <summary>Plain-language reason to show the user (when not Success).</summary>
    public string Message { get; init; } = string.Empty;
}

public class AuthService
{
    private const string GenericFailure =
        "We couldn't sign you in right now. Please try again in a moment.";

    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(
        HttpClient httpClient,
        IOptions<ApiSettings> settings,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Requests a sign-in code. The visitor's IP is forwarded so the API can
    /// rate-limit per person rather than per web server.
    /// </summary>
    public async Task<OtpRequestResult> GenerateOtpAsync(
        GenerateOtpRequest request)
    {
        try
        {
            using var response = await PostAsync("Auth/generate-otp", request);

            if (!response.IsSuccessStatusCode)
            {
                return new OtpRequestResult
                {
                    Message = await ReadMessageAsync(response)
                };
            }

            var result = await response.Content
                .ReadFromJsonAsync<GenerateOtpResult>();

            return new OtpRequestResult
            {
                Success = true,
                ExpiresInSeconds = result?.ExpiresInSeconds > 0
                    ? result.ExpiresInSeconds
                    : 300
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new OtpRequestResult { Message = GenericFailure };
        }
    }

    public async Task<VerifyOtpResponse?>
        VerifyOtpAsync(
            VerifyOtpRequest request)
    {
        try
        {
            using var response = await PostAsync("Auth/verify-otp", request);

            if (!response.IsSuccessStatusCode)
            {
                return new VerifyOtpResponse
                {
                    Success = false,
                    Message = await ReadMessageAsync(response)
                };
            }

            return await response.Content
                .ReadFromJsonAsync<VerifyOtpResponse>();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new VerifyOtpResponse { Success = false, Message = GenericFailure };
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object body)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl + path)
        {
            Content = JsonContent.Create(body)
        };

        ApiClient.AddClientIp(message, _httpContextAccessor.HttpContext);

        return await _httpClient.SendAsync(message);
    }

    /// <summary>The API's { "message": "..." } for 4xx responses; a generic text otherwise.</summary>
    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        if ((int)response.StatusCode >= 500)
            return GenericFailure;

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("message", out var text) &&
                text.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(text.GetString()))
            {
                return text.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Plain-text body; fall through.
        }

        return response.StatusCode == HttpStatusCode.TooManyRequests
            ? "Too many attempts. Please wait a few minutes and try again."
            : GenericFailure;
    }

    private class GenerateOtpResult
    {
        public int ExpiresInSeconds { get; set; }
    }
}
