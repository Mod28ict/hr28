using System.Net;
using System.Net.Http.Json;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>
/// Voter edits record which fields changed in the Audit Trail (2026-10-15): name and
/// National ID with old and new value, contact details and remarks by name only.
/// </summary>
[Collection(ApiCollection.Name)]
public class VoterEditAuditTests
{
    private readonly Hr28ApiFactory _factory;

    public VoterEditAuditTests(Hr28ApiFactory factory) => _factory = factory;

    private static object UpdateBody(Voter v, string fullName, string address, string remarks) => new
    {
        v.NationalId,
        FullName = fullName,
        Address = address,
        v.MobileNumber,
        v.ConstituencyId,
        v.IslandId,
        v.PoliticalPartyId,
        v.Gender,
        v.DateOfBirth,
        Remarks = remarks,
        v.SupportStatus
    };

    private async Task<string> LastVoterAuditAsync(Guid voterId)
    {
        await using var db = _factory.NewDbContext();
        return await db.AuditLogs
            .Where(a => a.EntityName == "Voter" && a.EntityId == voterId.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.Action)
            .FirstAsync();
    }

    [Fact]
    public async Task Changed_fields_are_listed_in_the_audit_entry()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var user = await _factory.Data.UserAsync(["Collector"], [(constituency, null)]);

        var response = await (await _factory.ClientForAsync(user))
            .PutAsJsonAsync($"api/Voters/{voter.Id}", UpdateBody(voter, "Changed Name", "New House", "met at the market"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var action = await LastVoterAuditAsync(voter.Id);
        Assert.Contains($"name \"{voter.FullName}\" → \"Changed Name\"", action);
        Assert.Contains("address", action);
        Assert.Contains("remarks", action);
        Assert.DoesNotContain("New House", action);
        Assert.DoesNotContain("market", action);
        Assert.DoesNotContain("mobile", action);
    }

    [Fact]
    public async Task Saving_without_changes_says_so()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var user = await _factory.Data.UserAsync(["Collector"], [(constituency, null)]);

        var response = await (await _factory.ClientForAsync(user))
            .PutAsJsonAsync($"api/Voters/{voter.Id}", UpdateBody(voter, voter.FullName, voter.Address ?? "", voter.Remarks ?? ""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Update (saved without changes)", await LastVoterAuditAsync(voter.Id));
    }
}
