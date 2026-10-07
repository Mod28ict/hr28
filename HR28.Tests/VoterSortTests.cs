using System.Net.Http.Json;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>Voters list: every column sorts on the server, in both directions.</summary>
[Collection(ApiCollection.Name)]
public class VoterSortTests
{
    private readonly Hr28ApiFactory _factory;

    public VoterSortTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record Row(Guid Id, string FullName, string NationalId, string IslandName, int PledgeCount);

    private sealed record Page(List<Row> Items, int TotalCount);

    private async Task<List<Row>> ListAsync(HttpClient client, Constituency c, string sort, bool desc) =>
        (await client.GetFromJsonAsync<Page>($"api/Voters?pageSize=100&constituencyId={c.Id}&sort={sort}&desc={desc}"))!.Items;

    [Fact]
    public async Task Island_sorts_A_to_Z_and_back_with_no_island_last()
    {
        var c = await _factory.Data.ConstituencyAsync();
        var a = await _factory.Data.IslandAsync(c);
        var b = await _factory.Data.IslandAsync(c);
        var onA = await _factory.Data.VoterAsync(c, a);
        var onB = await _factory.Data.VoterAsync(c, b);
        var none = await _factory.Data.VoterAsync(c);
        var client = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Collector"], [(c, null)]));

        var (first, second) = string.CompareOrdinal(a.Name, b.Name) < 0 ? (onA, onB) : (onB, onA);

        var up = await ListAsync(client, c, "island", false);
        Assert.Equal([first.Id, second.Id, none.Id], up.Select(r => r.Id));

        var down = await ListAsync(client, c, "island", true);
        Assert.Equal([second.Id, first.Id, none.Id], down.Select(r => r.Id));
    }

    [Fact]
    public async Task Every_column_sorts_without_errors_and_keeps_all_voters()
    {
        var c = await _factory.Data.ConstituencyAsync();
        for (var i = 0; i < 3; i++)
            await _factory.Data.VoterAsync(c);
        var client = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Collector"], [(c, null)]));

        foreach (var sort in new[] { "name", "nid", "phone", "island", "address", "party", "pledges", "status", "unknown" })
        foreach (var desc in new[] { false, true })
            Assert.Equal(3, (await ListAsync(client, c, sort, desc)).Count);
    }

    [Fact]
    public async Task Pledges_sort_largest_first_when_descending()
    {
        var c = await _factory.Data.ConstituencyAsync();
        var few = await _factory.Data.VoterAsync(c);
        var many = await _factory.Data.VoterAsync(c);
        var user = await _factory.Data.UserAsync(["Collector"], [(c, null)]);

        await using (var db = _factory.NewDbContext())
        {
            foreach (var (voter, count) in new[] { (few, 1), (many, 3) })
                for (var i = 0; i < count; i++)
                    db.Pledges.Add(new Pledge { Id = Guid.NewGuid(), VoterId = voter.Id, CreatedByUserId = user.Id, Title = "t", Description = "d", PledgeDate = DateTime.UtcNow, Status = "Pending", Priority = "Normal", ResolutionNotes = "" });
            await db.SaveChangesAsync();
        }

        var client = await _factory.ClientForAsync(user);

        Assert.Equal([many.Id, few.Id], (await ListAsync(client, c, "pledges", true)).Select(r => r.Id));
        Assert.Equal([few.Id, many.Id], (await ListAsync(client, c, "pledges", false)).Select(r => r.Id));
    }
}
