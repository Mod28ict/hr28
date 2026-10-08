using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>
/// Each client sees only their own branding: name, short name, tagline and logo come from
/// Settings (nothing about a client is built into the code), and the SMS uses them too.
/// </summary>
[Collection(ApiCollection.Name)]
public class BrandingTests
{
    private readonly Hr28ApiFactory _factory;

    public BrandingTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record Brand(string CampaignName, string ShortName, string Tagline, bool HasLogo, string LogoVersion);

    private sealed record Settings(string CampaignName, string? ShortName, string? Tagline, int OtpExpiryMinutes, int OtpMaxAttempts, int RememberDeviceDays);

    /// <summary>A valid 1×1 PNG.</summary>
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private async Task ResetAsync()
    {
        await using var db = _factory.NewDbContext();
        await db.SystemSettings.Where(s => s.Key == "CampaignName" || s.Key == "ShortName" || s.Key == "Tagline").ExecuteDeleteAsync();
        await db.BrandLogos.ExecuteDeleteAsync();
    }

    private async Task SaveAsync(HttpClient admin, string name, string? shortName, string? tagline)
    {
        var response = await admin.PutAsJsonAsync("api/Settings/system", new Settings(name, shortName, tagline, 5, 5, 30));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Branding_is_public_and_comes_only_from_the_clients_settings()
    {
        await ResetAsync();
        try
        {
            var anonymous = _factory.AnonymousClient();

            // A new client: neutral defaults, nothing from any other client.
            var fresh = await anonymous.GetFromJsonAsync<Brand>("api/Settings/branding");
            Assert.Equal("Campaign Intelligence", fresh!.CampaignName);
            Assert.Equal("CI", fresh.ShortName);
            Assert.False(fresh.HasLogo);

            var admin = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Super Administrator"]));

            // No short name saved: initials of the campaign name.
            await SaveAsync(admin, "Forward Together 2028", null, null);
            var derived = await anonymous.GetFromJsonAsync<Brand>("api/Settings/branding");
            Assert.Equal("FT28", derived!.ShortName);
            Assert.Equal("Campaign Intelligence Platform", derived.Tagline);

            await SaveAsync(admin, "Forward Together 2028", "FWD", "People first");
            var saved = await anonymous.GetFromJsonAsync<Brand>("api/Settings/branding");
            Assert.Equal(("Forward Together 2028", "FWD", "People first"), (saved!.CampaignName, saved.ShortName, saved.Tagline));
        }
        finally
        {
            await ResetAsync();
        }
    }

    [Fact]
    public async Task The_sign_in_SMS_uses_the_clients_short_name()
    {
        await ResetAsync();
        try
        {
            var admin = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Super Administrator"]));
            await SaveAsync(admin, "Forward Together 2028", "FWD", null);

            var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: "SMSBRAND");
            var response = await _factory.AnonymousClient().PostAsJsonAsync("api/Auth/generate-otp", new { authorizationCode = "SMSBRAND" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var sms = _factory.Sms.MessagesTo(user.MobileNumber).Last();
            Assert.StartsWith("FWD code: ", sms);
            Assert.DoesNotContain("HR28", sms);
        }
        finally
        {
            await ResetAsync();
        }
    }

    [Fact]
    public async Task The_administrator_uploads_replaces_and_removes_the_logo()
    {
        await ResetAsync();
        try
        {
            var admin = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Super Administrator"]));
            var anonymous = _factory.AnonymousClient();

            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("api/Settings/logo")).StatusCode);

            using (var form = new MultipartFormDataContent())
            {
                var file = new ByteArrayContent(TinyPng);
                file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                form.Add(file, "file", "logo.png");
                Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("api/Settings/logo", form)).StatusCode);
            }

            var brand = await anonymous.GetFromJsonAsync<Brand>("api/Settings/branding");
            Assert.True(brand!.HasLogo);

            var logo = await anonymous.GetAsync("api/Settings/logo");
            Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
            Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType);

            // Not an image: refused with a plain message.
            using (var form = new MultipartFormDataContent())
            {
                form.Add(new ByteArrayContent("not an image"u8.ToArray()), "file", "logo.png");
                Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("api/Settings/logo", form)).StatusCode);
            }

            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("api/Settings/logo")).StatusCode);
            Assert.False((await anonymous.GetFromJsonAsync<Brand>("api/Settings/branding"))!.HasLogo);

            await using var db = _factory.NewDbContext();
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Logo added"));
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Logo removed"));
        }
        finally
        {
            await ResetAsync();
        }
    }

    [Fact]
    public async Task Only_administrators_change_the_branding()
    {
        var collector = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Collector"]));

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(TinyPng), "file", "logo.png");

        Assert.Equal(HttpStatusCode.Forbidden, (await collector.PostAsync("api/Settings/logo", form)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await collector.DeleteAsync("api/Settings/logo")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await collector.PutAsJsonAsync("api/Settings/system", new Settings("Hijack", "HJ", null, 5, 5, 30))).StatusCode);
    }

    [Fact]
    public async Task A_short_name_must_be_short_and_plain()
    {
        var admin = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Super Administrator"]));

        var response = await admin.PutAsJsonAsync("api/Settings/system", new Settings("Forward Together 2028", "<script>", null, 5, 5, 30));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
