using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>
/// ASVS review M3 (2026-10-17): the API refuses over-long text with a plain message and
/// stores nothing.
/// </summary>
[Collection(ApiCollection.Name)]
public class InputLimitTests
{
    private readonly Hr28ApiFactory _factory;

    public InputLimitTests(Hr28ApiFactory factory) => _factory = factory;

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    [Fact]
    public async Task An_over_long_voter_name_is_refused_with_a_plain_message()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var user = await _factory.Data.UserAsync(["Collector"], [(constituency, null)]);

        var response = await (await _factory.ClientForAsync(user)).PutAsJsonAsync($"api/Voters/{voter.Id}", new
        {
            voter.NationalId,
            FullName = new string('A', 151),
            voter.Address,
            voter.MobileNumber,
            voter.ConstituencyId,
            voter.IslandId,
            Remarks = "",
            voter.SupportStatus
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Full name can be at most 150 characters.", await MessageAsync(response));

        await using var db = _factory.NewDbContext();
        Assert.Equal(voter.FullName, (await db.Voters.SingleAsync(v => v.Id == voter.Id)).FullName);
    }

    [Fact]
    public async Task Over_long_influencer_remarks_are_refused()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);

        var response = await (await _factory.ClientForAsync(admin)).PostAsJsonAsync("api/Influencers", new
        {
            NationalId = "A" + Random.Shared.Next(100000, 999999),
            FullName = "Limit Test",
            Address = "",
            ContactNumber = "",
            ConstituencyId = constituency.Id,
            Remarks = new string('x', 1001)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Remarks can be at most 1000 characters.", await MessageAsync(response));
    }
}
