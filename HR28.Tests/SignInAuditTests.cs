using System.Net;
using System.Net.Http.Json;
using HR28.API.Extensions;
using HR28.Domain.Entities;
using HR28.Infrastructure.Services;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>
/// ASVS review M2 (2026-10-17): every sign-in and every lockout is in the Audit Trail;
/// refusals go to the server log without anything that could forge log lines.
/// </summary>
[Collection(ApiCollection.Name)]
public class SignInAuditTests
{
    private readonly Hr28ApiFactory _factory;

    public SignInAuditTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record LoginResult(bool Success, string Message, string? KnownDeviceToken);

    private static string NewCode() => $"S{Guid.NewGuid():N}"[..8].ToUpperInvariant();

    private async Task<(HttpClient Client, string Code)> AskForCodeAsync(User user, string code)
    {
        await using (var db = _factory.NewDbContext())
            await db.OtpRequests.Where(o => o.UserId == user.Id).ExecuteDeleteAsync();

        var client = _factory.AnonymousClient();
        var request = await client.PostAsJsonAsync("api/Auth/generate-otp", new { authorizationCode = code });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        return (client, _factory.Sms.LastCodeFor(user.MobileNumber));
    }

    private async Task<List<string>> AuditOfAsync(Guid userId)
    {
        await using var db = _factory.NewDbContext();
        return await db.AuditLogs
            .Where(a => a.EntityName == "User" && a.EntityId == userId.ToString())
            .OrderBy(a => a.CreatedAt)
            .Select(a => a.Action)
            .ToListAsync();
    }

    [Fact]
    public async Task Every_sign_in_is_in_the_audit_trail()
    {
        var code = NewCode();
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: code);

        string? browserKey = null;

        for (var i = 0; i < 2; i++)
        {
            var (client, otp) = await AskForCodeAsync(user, code);
            var login = await (await client.PostAsJsonAsync("api/Auth/verify-otp", new
            {
                authorizationCode = code,
                otpCode = otp,
                deviceName = "Chrome on Android",
                knownDeviceToken = browserKey
            })).Content.ReadFromJsonAsync<LoginResult>();

            Assert.True(login!.Success);
            browserKey ??= login.KnownDeviceToken;
        }

        var audit = await AuditOfAsync(user.Id);
        Assert.Contains("First sign-in recorded on Chrome on Android", audit);
        Assert.Contains("Signed in on Chrome on Android", audit);
    }

    [Fact]
    public async Task A_lockout_is_in_the_audit_trail()
    {
        var code = NewCode();
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: code);

        // Ten wrong codes in a row (a fresh code every five tries) lock sign-in.
        for (var i = 0; i < AuthService.LockoutAfterFailedCodes; i++)
        {
            if (i % 5 == 0)
                await AskForCodeAsync(user, code);

            await _factory.AnonymousClient()
                .PostAsJsonAsync("api/Auth/verify-otp", new { authorizationCode = code, otpCode = "000000" });

            // The per-account wait between code requests would stop the next request.
            await using var db = _factory.NewDbContext();
            await db.OtpRequests.Where(o => o.UserId == user.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAt, o => o.CreatedAt.AddMinutes(-2)));
        }

        Assert.Contains(await AuditOfAsync(user.Id),
            a => a == $"Sign-in locked for {AuthService.LockoutMinutes} minutes after {AuthService.LockoutAfterFailedCodes} wrong codes");
    }

    [Fact]
    public void Logged_request_text_cannot_forge_log_lines()
    {
        Assert.Equal("/api/x_SECURITY: fake", SecurityLog.Clean("/api/x\nSECURITY: fake"));
        Assert.Equal(200, SecurityLog.Clean(new string('a', 500)).Length);
    }
}
