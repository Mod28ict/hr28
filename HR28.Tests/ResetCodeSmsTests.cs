using System.Net;
using System.Net.Http.Json;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>"Reset code" sends the new code by SMS and shows it once to the administrator.</summary>
[Collection(ApiCollection.Name)]
public class ResetCodeSmsTests
{
    private readonly Hr28ApiFactory _factory;

    public ResetCodeSmsTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record Reset(string AuthorizationCode, bool SmsSent, bool AccountInactive, string MaskedMobile);

    private async Task<Reset> ResetAsync(Guid userId)
    {
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var response = await (await _factory.ClientForAsync(admin)).PostAsync($"api/Users/{userId}/reset-code", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Reset>())!;
    }

    private async Task<string> AuditActionAsync(Guid userId)
    {
        await using var db = _factory.NewDbContext();
        return await db.AuditLogs.Where(a => a.EntityId == userId.ToString()).OrderByDescending(a => a.CreatedAt).Select(a => a.Action).FirstAsync();
    }

    [Fact]
    public async Task The_new_code_goes_to_the_person_by_SMS_and_is_shown_once()
    {
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: "OLDC0DE9");

        var result = await ResetAsync(user.Id);

        Assert.True(result.SmsSent);
        Assert.False(result.AccountInactive);
        Assert.Matches(@"^[A-Z2-9]{4}-[A-Z2-9]{4}$", result.AuthorizationCode);
        Assert.Equal("•••" + user.MobileNumber[^3..], result.MaskedMobile);

        var sms = _factory.Sms.MessagesTo(user.MobileNumber).Last();
        Assert.Contains($"new authorization code is {result.AuthorizationCode}", sms);
        Assert.Contains("Never share", sms);
        Assert.Contains("new code sent by SMS", await AuditActionAsync(user.Id));
    }

    [Fact]
    public async Task When_the_SMS_fails_the_administrator_still_gets_the_code()
    {
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: "OLDC0DE8");
        _factory.Sms.FailFor(user.MobileNumber);

        var result = await ResetAsync(user.Id);

        Assert.False(result.SmsSent);
        Assert.Matches(@"^[A-Z2-9]{4}-[A-Z2-9]{4}$", result.AuthorizationCode);
        Assert.Contains("SMS could not be sent", await AuditActionAsync(user.Id));
    }

    [Fact]
    public async Task An_inactive_account_gets_no_SMS()
    {
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: "OLDC0DE7", isActive: false);

        var result = await ResetAsync(user.Id);

        Assert.False(result.SmsSent);
        Assert.True(result.AccountInactive);
        Assert.Empty(_factory.Sms.MessagesTo(user.MobileNumber));
    }
}
