using System.Net;
using System.Net.Http.Json;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>
/// "Remember me on this device": a remembered browser skips the authorization code but
/// still needs the SMS code. Keys are stored hashed, expire, and are forgotten on a code
/// reset, on request, or when the feature is turned off.
/// </summary>
[Collection(ApiCollection.Name)]
public class RememberedDeviceTests
{
    private readonly Hr28ApiFactory _factory;

    public RememberedDeviceTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record LoginResult(bool Success, string Message, string? DeviceToken, DateTime? DeviceExpiresAt, bool DeviceNotRecognised);

    private sealed record Remembered(string FirstName, string MaskedMobile);

    private async Task<User> UserWithCodeAsync()
    {
        // Each test user gets its own code, so lookups by code never collide.
        var code = $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: code);
        _codes[user.Id] = code;
        return user;
    }

    private readonly Dictionary<Guid, string> _codes = new();

    /// <summary>Lets the next code request through the 60-second cooldown.</summary>
    private async Task ClearCodeRequestsAsync(Guid userId)
    {
        await using var db = _factory.NewDbContext();
        await db.OtpRequests.Where(o => o.UserId == userId).ExecuteDeleteAsync();
    }

    /// <summary>Signs in with the authorization code (or a device key) and the SMS code.</summary>
    private async Task<(HttpResponseMessage Request, LoginResult? Login)> SignInAsync(
        User user, string? deviceToken = null, bool remember = false)
    {
        await ClearCodeRequestsAsync(user.Id);

        var client = _factory.AnonymousClient();
        var code = deviceToken == null ? _codes[user.Id] : string.Empty;

        var request = await client.PostAsJsonAsync("api/Auth/generate-otp", new { authorizationCode = code, deviceToken });

        if (!request.IsSuccessStatusCode)
            return (request, null);

        var verify = await client.PostAsJsonAsync("api/Auth/verify-otp", new
        {
            authorizationCode = code,
            deviceToken,
            otpCode = _factory.Sms.LastCodeFor(user.MobileNumber),
            rememberDevice = remember,
            deviceName = "Chrome on Windows"
        });

        return (request, await verify.Content.ReadFromJsonAsync<LoginResult>());
    }

    [Fact]
    public async Task Ticking_remember_me_lets_the_device_skip_the_code_but_not_the_SMS()
    {
        var user = await UserWithCodeAsync();

        var (_, first) = await SignInAsync(user, remember: true);

        Assert.True(first!.Success);
        Assert.False(string.IsNullOrEmpty(first.DeviceToken));
        Assert.True(first.DeviceExpiresAt > DateTime.UtcNow.AddDays(29));

        // Only a hash is stored, never the key.
        await using (var db = _factory.NewDbContext())
        {
            var stored = await db.TrustedDevices.SingleAsync(d => d.UserId == user.Id);
            Assert.NotEqual(first.DeviceToken, stored.TokenHash);
            Assert.Equal("Chrome on Windows", stored.Name);
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == user.Id.ToString() && a.Action.StartsWith("Remembered a device")));
        }

        // Next time: no authorization code, still the SMS code, and no new key.
        var (_, second) = await SignInAsync(user, deviceToken: first.DeviceToken);

        Assert.True(second!.Success);
        Assert.Null(second.DeviceToken);
    }

    [Fact]
    public async Task Without_remember_me_no_device_is_remembered()
    {
        var user = await UserWithCodeAsync();

        var (_, login) = await SignInAsync(user);

        Assert.True(login!.Success);
        Assert.Null(login.DeviceToken);

        await using var db = _factory.NewDbContext();
        Assert.False(await db.TrustedDevices.AnyAsync(d => d.UserId == user.Id));
    }

    [Fact]
    public async Task The_sign_in_page_greets_a_remembered_device_and_nothing_else()
    {
        var user = await UserWithCodeAsync();
        var (_, login) = await SignInAsync(user, remember: true);
        var client = _factory.AnonymousClient();

        var known = await client.PostAsJsonAsync("api/Auth/device", new { deviceToken = login!.DeviceToken });
        var unknown = await client.PostAsJsonAsync("api/Auth/device", new { deviceToken = new string('A', 64) });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        var greeting = await known.Content.ReadFromJsonAsync<Remembered>();
        Assert.Equal(user.FullName.Split(' ')[0], greeting!.FirstName);
        Assert.EndsWith(user.MobileNumber[^3..], greeting.MaskedMobile);
        Assert.DoesNotContain(user.MobileNumber, greeting.MaskedMobile);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task An_unknown_device_key_cannot_request_a_code()
    {
        var response = await _factory.AnonymousClient().PostAsJsonAsync("api/Auth/generate-otp",
            new { authorizationCode = "", deviceToken = new string('B', 64) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("deviceNotRecognised\":true", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Resetting_the_code_forgets_every_remembered_device()
    {
        var user = await UserWithCodeAsync();
        var (_, login) = await SignInAsync(user, remember: true);

        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var reset = await (await _factory.ClientForAsync(admin)).PostAsync($"api/Users/{user.Id}/reset-code", null);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var (request, _) = await SignInAsync(user, deviceToken: login!.DeviceToken);
        Assert.Equal(HttpStatusCode.BadRequest, request.StatusCode);
    }

    [Fact]
    public async Task The_administrator_can_forget_a_persons_devices()
    {
        var user = await UserWithCodeAsync();
        var (_, login) = await SignInAsync(user, remember: true);

        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var adminClient = await _factory.ClientForAsync(admin);

        var listed = await adminClient.GetFromJsonAsync<UserRow>($"api/Users/{user.Id}");
        Assert.Equal(1, listed!.RememberedDevices);

        var forget = await adminClient.DeleteAsync($"api/Users/{user.Id}/devices");
        Assert.Equal(HttpStatusCode.OK, forget.StatusCode);

        var (request, _) = await SignInAsync(user, deviceToken: login!.DeviceToken);
        Assert.Equal(HttpStatusCode.BadRequest, request.StatusCode);
    }

    private sealed record UserRow(Guid Id, int RememberedDevices);

    [Fact]
    public async Task Not_you_forgets_this_device()
    {
        var user = await UserWithCodeAsync();
        var (_, login) = await SignInAsync(user, remember: true);
        var client = _factory.AnonymousClient();

        var forget = await client.PostAsJsonAsync("api/Auth/forget-device", new { deviceToken = login!.DeviceToken });
        Assert.Equal(HttpStatusCode.NoContent, forget.StatusCode);

        var known = await client.PostAsJsonAsync("api/Auth/device", new { deviceToken = login.DeviceToken });
        Assert.Equal(HttpStatusCode.NotFound, known.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_account_cannot_use_its_remembered_device()
    {
        var user = await UserWithCodeAsync();
        var (_, login) = await SignInAsync(user, remember: true);

        await using (var db = _factory.NewDbContext())
            await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

        var (request, _) = await SignInAsync(user, deviceToken: login!.DeviceToken);
        Assert.Equal(HttpStatusCode.BadRequest, request.StatusCode);
    }

    [Fact]
    public async Task Turning_the_setting_off_stops_remembered_devices()
    {
        var user = await UserWithCodeAsync();
        var (_, login) = await SignInAsync(user, remember: true);

        await using var db = _factory.NewDbContext();
        db.SystemSettings.Add(new SystemSetting { Key = "RememberDeviceDays", Value = "0", UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        try
        {
            var (request, _) = await SignInAsync(user, deviceToken: login!.DeviceToken);
            Assert.Equal(HttpStatusCode.BadRequest, request.StatusCode);

            // And "Remember me" no longer remembers anything.
            var (_, again) = await SignInAsync(user, remember: true);
            Assert.True(again!.Success);
            Assert.Null(again.DeviceToken);
        }
        finally
        {
            await db.SystemSettings.Where(s => s.Key == "RememberDeviceDays").ExecuteDeleteAsync();
        }
    }
}
