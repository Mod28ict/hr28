using System.Net;
using System.Net.Http.Json;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>
/// "Change support status" (Voters.Status): only people with this right may set or
/// change a voter's support status; Edit voters alone keeps it as it is.
/// </summary>
[Collection(ApiCollection.Name)]
public class VoterStatusRightTests
{
    private readonly Hr28ApiFactory _factory;

    public VoterStatusRightTests(Hr28ApiFactory factory) => _factory = factory;

    private async Task<string> StatusOfAsync(Guid voterId)
    {
        await using var db = _factory.NewDbContext();
        return (await db.Voters.SingleAsync(v => v.Id == voterId)).SupportStatus;
    }

    private static object UpdateBody(Voter v, string status) => new
    {
        v.NationalId,
        v.FullName,
        v.Address,
        v.MobileNumber,
        v.ConstituencyId,
        v.IslandId,
        v.PoliticalPartyId,
        v.Gender,
        v.DateOfBirth,
        Remarks = "",
        SupportStatus = status
    };

    [Fact]
    public async Task Without_the_right_the_status_popup_is_refused()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var collector = await _factory.Data.UserAsync(["Collector"], [(constituency, null)]);

        var response = await (await _factory.ClientForAsync(collector))
            .PutAsJsonAsync($"api/Voters/{voter.Id}/status", new { status = "Supporter" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Undecided", await StatusOfAsync(voter.Id));
    }

    [Fact]
    public async Task With_the_right_the_status_changes_and_is_audited()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var user = await _factory.Data.UserAsync(["Collector"], [(constituency, null)], extraRights: ["Voters.Status"]);

        var response = await (await _factory.ClientForAsync(user))
            .PutAsJsonAsync($"api/Voters/{voter.Id}/status", new { status = "Supporter" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Supporter", await StatusOfAsync(voter.Id));

        await using var db = _factory.NewDbContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == voter.Id.ToString() && a.Action.Contains("Supporter")));
    }

    [Fact]
    public async Task The_right_still_only_works_inside_the_users_areas()
    {
        var mine = await _factory.Data.ConstituencyAsync();
        var outsider = await _factory.Data.VoterAsync(await _factory.Data.ConstituencyAsync());
        var user = await _factory.Data.UserAsync(["Collector"], [(mine, null)], extraRights: ["Voters.Status"]);

        var response = await (await _factory.ClientForAsync(user))
            .PutAsJsonAsync($"api/Voters/{outsider.Id}/status", new { status = "Supporter" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Undecided", await StatusOfAsync(outsider.Id));
    }

    [Fact]
    public async Task Editing_a_voter_without_the_right_keeps_the_status()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var collector = await _factory.Data.UserAsync(["Collector"], [(constituency, null)]);

        var response = await (await _factory.ClientForAsync(collector))
            .PutAsJsonAsync($"api/Voters/{voter.Id}", UpdateBody(voter, "Opponent"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Undecided", await StatusOfAsync(voter.Id));
    }

    [Fact]
    public async Task Editing_a_voter_with_the_right_changes_the_status()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var user = await _factory.Data.UserAsync(["Collector"], [(constituency, null)], extraRights: ["Voters.Status"]);

        var response = await (await _factory.ClientForAsync(user))
            .PutAsJsonAsync($"api/Voters/{voter.Id}", UpdateBody(voter, "Opponent"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Opponent", await StatusOfAsync(voter.Id));
    }

    [Fact]
    public async Task The_right_appears_in_the_catalog_for_the_rights_grid()
    {
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);

        var body = await (await _factory.ClientForAsync(admin)).GetStringAsync("api/Permissions");

        Assert.Contains("Voters.Status", body);
    }
}
