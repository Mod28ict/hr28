using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HR28.Tests;

/// <summary>
/// Owner decision 2026-10-17 (ASVS review M1): voters one person can view per hour and per
/// day are capped (Settings → System, default 2,000 / 10,000), for every role. Reaching a
/// limit pauses viewing, is audited and SMSes the Administrators once.
/// </summary>
[Collection(ApiCollection.Name)]
public class VoterViewLimitTests
{
    private readonly Hr28ApiFactory _factory;

    public VoterViewLimitTests(Hr28ApiFactory factory) => _factory = factory;

    private async Task ViewAsync(User user, int voters)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IVoterViewMeter>().RecordAsync(user.Id, voters);
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    private async Task SetLimitsAsync(int? perHour, int? perDay)
    {
        await using var db = _factory.NewDbContext();
        await db.SystemSettings.Where(s => s.Key == "VoterViewsPerHour" || s.Key == "VoterViewsPerDay").ExecuteDeleteAsync();

        if (perHour.HasValue)
            db.SystemSettings.Add(new SystemSetting { Key = "VoterViewsPerHour", Value = perHour.ToString()!, UpdatedAt = DateTime.UtcNow });
        if (perDay.HasValue)
            db.SystemSettings.Add(new SystemSetting { Key = "VoterViewsPerDay", Value = perDay.ToString()!, UpdatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Reaching_the_hourly_limit_pauses_viewing_audits_and_alerts_administrators()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var collector = await _factory.Data.UserAsync(["Collector"], [(constituency, null)]);
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var client = await _factory.ClientForAsync(collector);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/Voters?party=all")).StatusCode);

        await ViewAsync(collector, 2000);

        var list = await client.GetAsync("api/Voters");
        Assert.Equal(HttpStatusCode.TooManyRequests, list.StatusCode);
        Assert.StartsWith("You have viewed 2,000 voters this hour, so viewing voters is paused until", await MessageAsync(list));

        var lookup = await client.GetAsync($"api/Voters/by-national-id/{voter.NationalId}");
        Assert.Equal(HttpStatusCode.TooManyRequests, lookup.StatusCode);

        // Opening a record the person already has is not affected.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"api/Voters/{voter.Id}")).StatusCode);

        await using var db = _factory.NewDbContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == collector.Id.ToString() && a.Action.StartsWith("Voter viewing paused")));
        Assert.Contains(_factory.Sms.MessagesTo(admin.MobileNumber), m => m.Contains($"{collector.FullName} viewed") && m.Contains("paused"));

        // One alert per hour, however much more is tried.
        var alerts = _factory.Sms.MessagesTo(admin.MobileNumber).Count;
        await ViewAsync(collector, 50);
        Assert.Equal(alerts, _factory.Sms.MessagesTo(admin.MobileNumber).Count);
    }

    [Fact]
    public async Task Administrators_are_limited_too()
    {
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        await ViewAsync(admin, 2000);

        var response = await (await _factory.ClientForAsync(admin)).GetAsync("api/Voters");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task The_daily_limit_says_tomorrow_and_raising_a_limit_lifts_the_pause()
    {
        var collector = await _factory.Data.UserAsync(["Collector"]);
        var client = await _factory.ClientForAsync(collector);

        try
        {
            await SetLimitsAsync(perHour: 500, perDay: 500);
            await ViewAsync(collector, 500);

            var paused = await client.GetAsync("api/Voters");
            Assert.Equal(HttpStatusCode.TooManyRequests, paused.StatusCode);
            Assert.Contains("today, so viewing voters is paused until tomorrow", await MessageAsync(paused));

            await SetLimitsAsync(perHour: 2000, perDay: 10000);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/Voters")).StatusCode);
        }
        finally
        {
            await SetLimitsAsync(null, null);
        }
    }

    [Fact]
    public async Task Limits_are_set_and_checked_in_system_settings()
    {
        var admin = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Super Administrator"]));

        try
        {
            var lower = await admin.PutAsJsonAsync("api/Settings/system", new
            {
                campaignName = "Limit Test Campaign", otpExpiryMinutes = 5, otpMaxAttempts = 5, rememberDeviceDays = 30,
                voterViewsPerHour = 3000, voterViewsPerDay = 1000
            });
            Assert.Equal(HttpStatusCode.BadRequest, lower.StatusCode);
            Assert.Equal("The daily limit can't be lower than the hourly limit.", await MessageAsync(lower));

            var saved = await admin.PutAsJsonAsync("api/Settings/system", new
            {
                campaignName = "Limit Test Campaign", otpExpiryMinutes = 5, otpMaxAttempts = 5, rememberDeviceDays = 30,
                voterViewsPerHour = 3000, voterViewsPerDay = 15000
            });
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

            var settings = await admin.GetFromJsonAsync<JsonElement>("api/Settings/system");
            Assert.Equal(3000, settings.GetProperty("voterViewsPerHour").GetInt32());
            Assert.Equal(15000, settings.GetProperty("voterViewsPerDay").GetInt32());

            await using var db = _factory.NewDbContext();
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == "Voters a person can view per day"));
        }
        finally
        {
            await SetLimitsAsync(null, null);
            await using var db = _factory.NewDbContext();
            await db.SystemSettings.Where(s => s.Key == "CampaignName").ExecuteDeleteAsync();
        }
    }
}
