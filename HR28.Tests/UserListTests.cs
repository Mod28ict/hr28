using System.Net;
using System.Net.Http.Json;
using HR28.Tests.Infrastructure;

namespace HR28.Tests;

/// <summary>The Users list: server-side pages, search and status filter (Administrator only).</summary>
[Collection(ApiCollection.Name)]
public class UserListTests
{
    private readonly Hr28ApiFactory _factory;

    public UserListTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record UserRow(Guid Id, string NationalId, bool IsActive);

    private sealed record UserPage(List<UserRow> Items, int Page, int PageSize, int TotalCount, int TotalUsers, int ActiveUsers, int InactiveUsers);

    [Fact]
    public async Task Users_come_in_pages_with_totals_for_everyone()
    {
        for (var i = 0; i < 12; i++)
            await _factory.Data.UserAsync(["Collector"]);

        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var client = await _factory.ClientForAsync(admin);

        var first = await client.GetFromJsonAsync<UserPage>("api/Users/search?page=1&pageSize=10");
        var second = await client.GetFromJsonAsync<UserPage>("api/Users/search?page=2&pageSize=10");

        Assert.Equal(10, first!.Items.Count);
        Assert.Equal(first.TotalCount, second!.TotalCount);
        Assert.True(first.TotalCount >= 13);
        Assert.Empty(first.Items.Select(u => u.Id).Intersect(second.Items.Select(u => u.Id)));
        Assert.Equal(first.TotalUsers, first.ActiveUsers + first.InactiveUsers);
    }

    [Fact]
    public async Task Search_and_status_narrow_the_list()
    {
        var active = await _factory.Data.UserAsync(["Collector"]);
        var inactive = await _factory.Data.UserAsync(["Collector"], isActive: false);
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var client = await _factory.ClientForAsync(admin);

        var byId = await client.GetFromJsonAsync<UserPage>($"api/Users/search?search={active.NationalId}");
        Assert.Equal(active.Id, Assert.Single(byId!.Items).Id);

        var inactiveOnly = await client.GetFromJsonAsync<UserPage>($"api/Users/search?search={inactive.NationalId}&status=active");
        Assert.Empty(inactiveOnly!.Items);

        var found = await client.GetFromJsonAsync<UserPage>($"api/Users/search?search={inactive.NationalId}&status=inactive");
        Assert.False(Assert.Single(found!.Items).IsActive);
    }

    [Fact]
    public async Task Only_the_administrator_can_list_users()
    {
        var national = await _factory.Data.UserAsync(["National Administrator"]);

        var response = await (await _factory.ClientForAsync(national)).GetAsync("api/Users/search");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
