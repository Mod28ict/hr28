using System.Net.Http.Json;
using System.Text.Json;
using HR28.Web.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HR28.Web.Services;

/// <summary>The client's branding as shown on every page (Settings → System → Branding).</summary>
public class Branding
{
    /// <summary>e.g. "Hithaai Roohun 2028".</summary>
    public string CampaignName { get; set; } = "Campaign Intelligence";

    /// <summary>e.g. "HR28" — logo mark, page titles.</summary>
    public string ShortName { get; set; } = "CI";

    public string Tagline { get; set; } = "Campaign Intelligence Platform";

    public bool HasLogo { get; set; }

    public string LogoVersion { get; set; } = string.Empty;

    /// <summary>
    /// Initials from the campaign name, as the server makes them when no short name is
    /// saved ("Hithaai Roohun 2028" → "HR28").
    /// </summary>
    public static string InitialsOf(string? campaignName)
    {
        var parts = (campaignName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.All(char.IsDigit) ? (w.Length > 2 ? w[^2..] : w) : char.ToUpperInvariant(w[0]).ToString())
            .Where(p => p.Length > 0 && p.All(char.IsLetterOrDigit))
            .Take(4);

        var initials = string.Concat(parts);
        return initials.Length > 0 ? initials : "HQ";
    }

    /// <summary>Shorter initials get bigger letters in the square logo mark.</summary>
    public string MarkSizeClass => ShortName.Length switch { <= 3 => "", <= 5 => "is-small", _ => "is-tiny" };
}

/// <summary>
/// Reads the client's branding from the API (no sign-in needed: the sign-in pages show
/// it) and keeps it for a minute. Nothing about any client is built into the code; each
/// deployment shows only its own name, short name, tagline and logo.
/// </summary>
public class BrandingService
{
    private const string CacheKey = "hr28.branding";
    private const string LogoCacheKey = "hr28.branding.logo";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly IMemoryCache _cache;

    public BrandingService(HttpClient httpClient, IOptions<ApiSettings> settings, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _cache = cache;
    }

    public async Task<Branding> GetAsync()
    {
        if (_cache.TryGetValue(CacheKey, out Branding? cached) && cached != null)
            return cached;

        try
        {
            var branding = await _httpClient.GetFromJsonAsync<Branding>(_settings.BaseUrl + "Settings/branding");

            if (branding != null)
            {
                _cache.Set(CacheKey, branding, CacheFor);
                return branding;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Neutral defaults below; try again on the next page.
        }

        return new Branding();
    }

    /// <summary>The logo image, or null when there is none (or the API can't be reached).</summary>
    public async Task<(byte[] Content, string ContentType)?> GetLogoAsync()
    {
        var version = (await GetAsync()).LogoVersion;

        if (_cache.TryGetValue(LogoCacheKey + version, out (byte[], string) cached))
            return cached;

        try
        {
            using var response = await _httpClient.GetAsync(_settings.BaseUrl + "Settings/logo");

            if (!response.IsSuccessStatusCode)
                return null;

            var logo = (await response.Content.ReadAsByteArrayAsync(),
                response.Content.Headers.ContentType?.MediaType ?? "image/png");

            _cache.Set(LogoCacheKey + version, logo, TimeSpan.FromMinutes(10));
            return logo;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>After an administrator changes the branding: show it at once on this server.</summary>
    public void Forget() => _cache.Remove(CacheKey);
}
