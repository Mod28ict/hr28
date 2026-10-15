using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HR28.Application.Common;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>
/// Owner, 2026-10-15: "confirm it's you" (a fresh SMS code before sensitive actions) and an
/// SMS when an account signs in from a browser it hasn't used before.
/// </summary>
[Collection(ApiCollection.Name)]
public class StepUpAndSignInAlertTests
{
    private readonly Hr28ApiFactory _factory;

    public StepUpAndSignInAlertTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record SendResult(bool Success, string Message, string MaskedMobile);
    private sealed record VerifyResult(bool Success, string Message, string Token);
    private sealed record LoginResult(bool Success, string Message, string? KnownDeviceToken);

    private async Task ClearCodeRequestsAsync(Guid userId)
    {
        await using var db = _factory.NewDbContext();
        await db.OtpRequests.Where(o => o.UserId == userId).ExecuteDeleteAsync();
    }

    // ---------------------------------------------------------------- confirm it's you

    [Fact]
    public async Task Sensitive_actions_need_a_recent_confirmation()
    {
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);

        var notConfirmed = await (await _factory.ClientForAsync(admin, stepUp: false)).GetAsync("api/Users");
        Assert.Equal(HttpStatusCode.Forbidden, notConfirmed.StatusCode);
        Assert.True(notConfirmed.Headers.Contains(StepUpRules.RequiredHeader));
        Assert.Contains("stepUpRequired", await notConfirmed.Content.ReadAsStringAsync());

        var tooOld = await (await _factory.ClientForAsync(admin, steppedUpAt: DateTime.UtcNow.AddMinutes(-16))).GetAsync("api/Users");
        Assert.Equal(HttpStatusCode.Forbidden, tooOld.StatusCode);

        var fresh = await (await _factory.ClientForAsync(admin)).GetAsync("api/Users");
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    [Fact]
    public async Task Everyday_work_needs_no_confirmation()
    {
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var client = await _factory.ClientForAsync(admin, stepUp: false);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/Constituencies")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/Settings/system")).StatusCode);
    }

    [Fact]
    public async Task People_without_access_still_get_a_plain_refusal()
    {
        var collector = await _factory.Data.UserAsync(["Collector"]);

        var response = await (await _factory.ClientForAsync(collector, stepUp: false)).GetAsync("api/Users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains(StepUpRules.RequiredHeader));
    }

    [Fact]
    public async Task The_SMS_code_confirms_and_unlocks_sensitive_actions()
    {
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        await ClearCodeRequestsAsync(admin.Id);
        var client = await _factory.ClientForAsync(admin, stepUp: false);

        var send = await client.PostAsync("api/Auth/step-up/send", null);
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        Assert.Equal("•••" + admin.MobileNumber[^3..], (await send.Content.ReadFromJsonAsync<SendResult>())!.MaskedMobile);

        var sms = _factory.Sms.MessagesTo(admin.MobileNumber).Last();
        Assert.Contains("confirmation code", sms);

        var sent = _factory.Sms.LastCodeFor(admin.MobileNumber);
        var wrong = await client.PostAsJsonAsync("api/Auth/step-up/verify", new { code = sent == "000000" ? "111111" : "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("not correct", (await wrong.Content.ReadFromJsonAsync<VerifyResult>())!.Message);

        var verify = await client.PostAsJsonAsync("api/Auth/step-up/verify", new { code = sent });
        var result = await verify.Content.ReadFromJsonAsync<VerifyResult>();
        Assert.True(result!.Success);

        var confirmed = _factory.AnonymousClient();
        confirmed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);
        Assert.Equal(HttpStatusCode.OK, (await confirmed.GetAsync("api/Users")).StatusCode);

        // The confirmation survives the quiet token refresh while working.
        var refreshed = await confirmed.PostAsync("api/Auth/refresh", null);
        var token = (await refreshed.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["token"];
        confirmed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await confirmed.GetAsync("api/Users")).StatusCode);

        await using var db = _factory.NewDbContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.UserId == admin.Id && a.Action.Contains("step-up")));
    }

    [Fact]
    public async Task A_confirmation_code_cannot_be_used_to_sign_in()
    {
        var code = $"S{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var admin = await _factory.Data.UserAsync(["Super Administrator"], authorizationCode: code);
        await ClearCodeRequestsAsync(admin.Id);

        await (await _factory.ClientForAsync(admin, stepUp: false)).PostAsync("api/Auth/step-up/send", null);
        var stepUpCode = _factory.Sms.LastCodeFor(admin.MobileNumber);

        var signIn = await _factory.AnonymousClient().PostAsJsonAsync("api/Auth/verify-otp",
            new { authorizationCode = code, otpCode = stepUpCode });
        Assert.False((await signIn.Content.ReadFromJsonAsync<LoginResult>())!.Success);
    }

    // ---------------------------------------------------------------- sign-in alerts

    private async Task<LoginResult> SignInAsync(User user, string code, string? knownDeviceToken)
    {
        await ClearCodeRequestsAsync(user.Id);
        var client = _factory.AnonymousClient();

        var request = await client.PostAsJsonAsync("api/Auth/generate-otp", new { authorizationCode = code });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        var verify = await client.PostAsJsonAsync("api/Auth/verify-otp", new
        {
            authorizationCode = code,
            otpCode = _factory.Sms.LastCodeFor(user.MobileNumber),
            deviceName = "Chrome on Android",
            knownDeviceToken
        });

        return (await verify.Content.ReadFromJsonAsync<LoginResult>())!;
    }

    [Fact]
    public async Task A_new_browser_gets_an_alert_and_a_known_one_does_not()
    {
        var code = $"K{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: code);

        // First browser ever: recorded quietly.
        var first = await SignInAsync(user, code, null);
        Assert.True(first.Success);
        Assert.False(string.IsNullOrEmpty(first.KnownDeviceToken));
        Assert.DoesNotContain(_factory.Sms.MessagesTo(user.MobileNumber), m => m.Contains("new sign-in"));

        // Same browser again: no alert, no new key.
        var again = await SignInAsync(user, code, first.KnownDeviceToken);
        Assert.True(again.Success);
        Assert.Null(again.KnownDeviceToken);
        Assert.DoesNotContain(_factory.Sms.MessagesTo(user.MobileNumber), m => m.Contains("new sign-in"));

        // Another browser: alert by SMS, recorded in the audit trail.
        var other = await SignInAsync(user, code, null);
        Assert.True(other.Success);
        var alert = _factory.Sms.MessagesTo(user.MobileNumber).Last();
        Assert.Contains("new sign-in to your account", alert);
        Assert.Contains("Chrome on Android", alert);
        Assert.Contains("contact your administrator", alert);

        await using var db = _factory.NewDbContext();
        Assert.Equal(2, await db.KnownDevices.CountAsync(d => d.UserId == user.Id));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.UserId == user.Id && a.Action.StartsWith("Signed in from a new device")));
        Assert.DoesNotContain(await db.KnownDevices.Where(d => d.UserId == user.Id).Select(d => d.KeyHash).ToListAsync(),
            h => h == first.KnownDeviceToken);
    }
}
