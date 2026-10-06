using System.Net;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests.Infrastructure;

/// <summary>Checks the test host itself: database built from the migrations, app started, sign-in token accepted.</summary>
[Collection(ApiCollection.Name)]
public class InfrastructureSmokeTests
{
    private readonly Hr28ApiFactory _factory;

    public InfrastructureSmokeTests(Hr28ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Built_in_roles_exist_with_their_default_rights()
    {
        await using var db = _factory.NewDbContext();

        var rights = await db.RolePermissions
            .Where(rp => rp.Role.Name == "Collector")
            .Select(rp => rp.Permission)
            .ToListAsync();

        Assert.Contains("Voters.View", rights);
        Assert.Contains("Encounters.Add", rights);
        Assert.DoesNotContain("Voters.Delete", rights);
    }

    [Fact]
    public async Task Requests_without_a_token_are_refused()
    {
        var response = await _factory.AnonymousClient().GetAsync("api/Voters");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_signed_in_user_can_open_a_voter_in_their_area()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var user = await _factory.Data.UserAsync(["Collector"], [(constituency, null)]);

        var response = await (await _factory.ClientForAsync(user)).GetAsync($"api/Voters/{voter.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
