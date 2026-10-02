using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HR28.Web.Models;
using Microsoft.Extensions.Options;

namespace HR28.Web.Services;

/// <summary>
/// Result of an API call with a user-friendly message on failure.
/// </summary>
public class ApiResult<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public HttpStatusCode StatusCode { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>The session token is missing or expired; the user must sign in again.</summary>
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
}

public class ApiFile
{
    public byte[] Content { get; init; } = Array.Empty<byte>();

    public string ContentType { get; init; } = "application/octet-stream";

    public string FileName { get; init; } = string.Empty;
}

/// <summary>
/// Thin JSON client for the HR28 API. Never throws for HTTP errors:
/// failures come back as <see cref="ApiResult{T}"/> with a safe message.
/// </summary>
public class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly ILogger<ApiClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiClient(
        HttpClient httpClient,
        IOptions<ApiSettings> settings,
        ILogger<ApiClient> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Passes the visitor's IP to the API (which trusts it only from known
    /// proxies) so rate limits apply per person, not per web server.
    /// </summary>
    public static void AddClientIp(HttpRequestMessage request, HttpContext? context)
    {
        var ip = context?.Connection.RemoteIpAddress;

        if (ip == null)
            return;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        request.Headers.TryAddWithoutValidation("X-Forwarded-For", ip.ToString());
    }

    public Task<ApiResult<T>> GetAsync<T>(string path, string? token) =>
        SendAsync<T>(HttpMethod.Get, path, null, token);

    /// <summary>Downloads a file (e.g. a CSV export) as raw bytes with its name and type.</summary>
    public async Task<ApiResult<ApiFile>> GetFileAsync(string path, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ApiResult<ApiFile>
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Message = "Your session has ended. Please sign in again."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _settings.BaseUrl + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            AddClientIp(request, _httpContextAccessor.HttpContext);

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult<ApiFile>
                {
                    StatusCode = response.StatusCode,
                    Message = await ReadMessageAsync(response)
                };
            }

            return new ApiResult<ApiFile>
            {
                Success = true,
                StatusCode = response.StatusCode,
                Data = new ApiFile
                {
                    Content = await response.Content.ReadAsByteArrayAsync(),
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
                    FileName = response.Content.Headers.ContentDisposition?.FileNameStar
                               ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                               ?? "hr28-report"
                }
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "File download {Path} failed", path);

            return new ApiResult<ApiFile>
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Message = "The service is not responding. Please try again shortly."
            };
        }
    }

    /// <summary>Uploads one file as multipart form data (field name "file").</summary>
    public async Task<ApiResult<T>> PostFileAsync<T>(string path, IFormFile file, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ApiResult<T>
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Message = "Your session has ended. Please sign in again."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            AddClientIp(request, _httpContextAccessor.HttpContext);

            await using var stream = file.OpenReadStream();
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);

            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(file.FileName));
            request.Content = content;

            using var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return new ApiResult<T>
                {
                    Success = true,
                    StatusCode = response.StatusCode,
                    Data = await response.Content.ReadFromJsonAsync<T>(JsonOptions)
                };
            }

            return new ApiResult<T>
            {
                StatusCode = response.StatusCode,
                Message = await ReadMessageAsync(response)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "File upload {Path} failed", path);

            // A timeout does not mean the server stopped: a large import may still finish.
            return new ApiResult<T>
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Message = ex is TaskCanceledException
                    ? "The upload is taking longer than expected and may still finish. Check the Audit Trail in a few minutes before uploading the file again."
                    : "The upload did not finish. Please try again shortly."
            };
        }
    }

    public Task<ApiResult<T>> PostAsync<T>(string path, object body, string? token) =>
        SendAsync<T>(HttpMethod.Post, path, body, token);

    public Task<ApiResult<object>> DeleteAsync(string path, string? token) =>
        SendAsync<object>(HttpMethod.Delete, path, null, token);

    public Task<ApiResult<T>> PutAsync<T>(string path, object body, string? token) =>
        SendAsync<T>(HttpMethod.Put, path, body, token);

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ApiResult<T>
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Message = "Your session has ended. Please sign in again."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(method, _settings.BaseUrl + path);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            AddClientIp(request, _httpContextAccessor.HttpContext);

            if (body != null)
                request.Content = JsonContent.Create(body, options: JsonOptions);

            using var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var hasBody = response.Content.Headers.ContentLength is null or > 0 &&
                              response.StatusCode != HttpStatusCode.NoContent;

                return new ApiResult<T>
                {
                    Success = true,
                    StatusCode = response.StatusCode,
                    Data = hasBody
                        ? await response.Content.ReadFromJsonAsync<T>(JsonOptions)
                        : default
                };
            }

            return new ApiResult<T>
            {
                StatusCode = response.StatusCode,
                Message = await ReadMessageAsync(response)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "API call {Method} {Path} failed", method, path);

            return new ApiResult<T>
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Message = "The service is not responding. Please try again shortly."
            };
        }
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                return "Your session has ended. Please sign in again.";
            case HttpStatusCode.NotFound:
                return "The requested record was not found.";
        }

        // The API returns { "message": "..." } for business-rule and access errors.
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String &&
                (int)response.StatusCode < 500)
            {
                return message.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // Not JSON; fall through to a generic message.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.TooManyRequests => "Too many requests. Please wait a few minutes and try again.",
            _ => "Something went wrong. Please try again."
        };
    }
}
