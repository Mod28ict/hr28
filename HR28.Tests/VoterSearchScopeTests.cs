using System.Net;
using System.Net.Http.Json;
using HR28.Tests.Infrastructure;

namespace HR28.Tests;

/// <summary>
/// Voter searches and the Voters list stay inside the user's areas. An administrator
/// with areas assigned searches only those; without areas, the whole registry.
/// Opening a voter is not affected (owner decision, 2026-10-06).
/// </summary>
[Collection(ApiCollection.Name)]
public class VoterSearchScopeTests
{
    private readonly Hr28ApiFactory _factory;

    public VoterSearchScopeTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record VoterRow(Guid Id, string NationalId);

    private sealed record MyAccount(string SearchArea);

    private sealed record Page(List<VoterRow> Items, int TotalCount);

    private static async Task<List<Guid>> ListAsync(HttpClient client, string term) =>
        (await client.GetFromJsonAsync<Page>($"api/Voters?pageSize=100&searchTerm={Uri.EscapeDataString(term)}"))!
            .Items.Select(v => v.Id).ToList();

    private static async Task<List<Guid>> SearchAsync(HttpClient client, string term) =>
        (await client.GetFromJsonAsync<List<VoterRow>>($"api/Voters/search?searchTerm={Uri.EscapeDataString(term)}"))!
            .Select(v => v.Id).ToList();

    [Theory]
    [InlineData("Super Administrator")]
    [InlineData("National Administrator")]
    public async Task An_administrator_with_an_area_searches_only_that_area(string role)
    {
        var mine = await _factory.Data.ConstituencyAsync();
        var other = await _factory.Data.ConstituencyAsync();
        var inside = await _factory.Data.VoterAsync(mine);
        var outside = await _factory.Data.VoterAsync(other);
        var admin = await _factory.Data.UserAsync([role], [(mine, null)]);

        var client = await _factory.ClientForAsync(admin);

        Assert.Contains(inside.Id, await ListAsync(client, inside.NationalId));
        Assert.Empty(await ListAsync(client, outside.NationalId));
        Assert.Contains(inside.Id, await SearchAsync(client, inside.NationalId));
        Assert.Empty(await SearchAsync(client, outside.NationalId));
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"api/Voters/by-national-id/{outside.NationalId}")).StatusCode);

        // Opening a record outside the areas still works for an administrator.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"api/Voters/{outside.Id}")).StatusCode);
    }

    [Fact]
    public async Task An_administrator_without_areas_searches_the_whole_registry()
    {
        var a = await _factory.Data.VoterAsync(await _factory.Data.ConstituencyAsync());
        var b = await _factory.Data.VoterAsync(await _factory.Data.ConstituencyAsync());
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);

        var client = await _factory.ClientForAsync(admin);

        Assert.Contains(a.Id, await ListAsync(client, a.NationalId));
        Assert.Contains(b.Id, await ListAsync(client, b.NationalId));
        Assert.Contains(b.Id, await SearchAsync(client, b.NationalId));
    }

    [Fact]
    public async Task Other_roles_search_only_their_areas()
    {
        var mine = await _factory.Data.ConstituencyAsync();
        var other = await _factory.Data.ConstituencyAsync();
        var inside = await _factory.Data.VoterAsync(mine);
        var outside = await _factory.Data.VoterAsync(other);
        var collector = await _factory.Data.UserAsync(["Collector"], [(mine, null)]);

        var client = await _factory.ClientForAsync(collector);

        Assert.Contains(inside.Id, await ListAsync(client, inside.NationalId));
        Assert.Empty(await ListAsync(client, outside.NationalId));
        Assert.Empty(await SearchAsync(client, outside.NationalId));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/Voters/{outside.Id}")).StatusCode);
    }

    [Fact]
    public async Task Someone_with_no_areas_and_no_administrator_role_finds_nobody()
    {
        var voter = await _factory.Data.VoterAsync(await _factory.Data.ConstituencyAsync());
        var collector = await _factory.Data.UserAsync(["Collector"]);

        var client = await _factory.ClientForAsync(collector);

        Assert.Empty(await ListAsync(client, voter.NationalId));
        Assert.Empty(await SearchAsync(client, voter.NationalId));
    }

    [Fact]
    public async Task The_account_says_where_searches_run_so_the_screens_can_explain_it()
    {
        var constituency = await _factory.Data.ConstituencyAsync();

        async Task<string> SearchAreaOf(string role, bool withArea)
        {
            var user = await _factory.Data.UserAsync([role], withArea ? [(constituency, null)] : null);
            var me = await (await _factory.ClientForAsync(user)).GetFromJsonAsync<MyAccount>("api/Settings/me");
            return me!.SearchArea;
        }

        Assert.Equal("Areas", await SearchAreaOf("Collector", withArea: true));
        Assert.Equal("None", await SearchAreaOf("Collector", withArea: false));
        Assert.Equal("Areas", await SearchAreaOf("National Administrator", withArea: true));
        Assert.Equal("All", await SearchAreaOf("National Administrator", withArea: false));
        Assert.Equal("All", await SearchAreaOf("Super Administrator", withArea: false));
    }
}
