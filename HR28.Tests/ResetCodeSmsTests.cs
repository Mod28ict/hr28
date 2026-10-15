using System.Net;
using System.Net.Http.Json;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>
/// "Reset code" (owner, 2026-10-15): the new code goes only to the person by SMS;
/// administrators never see it.
/// </summary>
[Collection(ApiCollection.Name)]
public class ResetCodeSmsTests
{
    private readonly Hr28ApiFactory _factory;

    public ResetCodeSmsTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record Reset(bool SmsSent, bool AccountInactive, string MaskedMobile);

    private async Task<(Reset Result, string Raw)> ResetAsync(Guid userId)
    {
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var response = await (await _factory.ClientForAsync(admin)).PostAsync($"api/Users/{userId}/reset-code", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        return ((await response.Content.ReadFromJsonAsync<Reset>())!, raw);
    }

    private async Task<(string? Hash, string Action)> StateAsync(Guid userId)
    {
        await using var db = _factory.NewDbContext();
        var hash = await db.Users.Where(u => u.Id == userId).Select(u => u.AuthorizationCodeHash).SingleAsync();
        var action = await db.AuditLogs.Where(a => a.EntityId == userId.ToString())
            .OrderByDescending(a => a.CreatedAt).Select(a => a.Action).FirstAsync();
        return (hash, action);
    }

    [Fact]
    public async Task The_new_code_goes_only_to_the_person_by_SMS()
    {
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: "OLDC0DE9");

        var (result, raw) = await ResetAsync(user.Id);

        Assert.True(result.SmsSent);
        Assert.Equal("•••" + user.MobileNumber[^3..], result.MaskedMobile);

        var sms = _factory.Sms.MessagesTo(user.MobileNumber).Last();
        var code = System.Text.RegularExpressions.Regex.Match(sms, @"[A-Z2-9]{4}-[A-Z2-9]{4}").Value;
        Assert.NotEmpty(code);
        Assert.Contains("Never share", sms);

        // The administrator's answer never contains the code, with or without the dash.
        Assert.DoesNotContain(code, raw);
        Assert.DoesNotContain(code.Replace("-", ""), raw);
        Assert.DoesNotContain("authorizationCode", raw, StringComparison.OrdinalIgnoreCase);

        var (hash, action) = await StateAsync(user.Id);
        Assert.Equal(_factory.HashAuthorizationCode(code), hash);
        Assert.Contains("new code sent by SMS", action);
    }

    [Fact]
    public async Task When_the_SMS_fails_no_code_is_left_working()
    {
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: "OLDC0DE8");
        _factory.Sms.FailFor(user.MobileNumber);

        var (result, _) = await ResetAsync(user.Id);

        Assert.False(result.SmsSent);
        var (hash, action) = await StateAsync(user.Id);
        Assert.Null(hash);
        Assert.Contains("SMS could not be sent", action);
    }

    [Fact]
    public async Task An_inactive_account_gets_its_new_code_with_activation()
    {
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: "OLDC0DE7", isActive: false);

        var (result, _) = await ResetAsync(user.Id);

        Assert.True(result.AccountInactive);
        Assert.False(result.SmsSent);
        Assert.Empty(_factory.Sms.MessagesTo(user.MobileNumber));
        Assert.Null((await StateAsync(user.Id)).Hash);
    }
}
