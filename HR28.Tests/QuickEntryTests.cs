using System.Net;
using System.Net.Http.Json;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>
/// Quick entry: fetching a voter by ID card (only inside the user's areas) and the
/// per-role start page that lets a role skip the Dashboard.
/// </summary>
[Collection(ApiCollection.Name)]
public class QuickEntryTests
{
    private readonly Hr28ApiFactory _factory;

    public QuickEntryTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record VoterResult(Guid Id, string NationalId, string FullName, bool HasPhoto);

    private sealed record MyAccount(string StartPage, List<string> Permissions);

    /// <summary>A custom role with the given rights and start page.</summary>
    private async Task<Role> CustomRoleAsync(string startPage, params string[] rights)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = $"Test Role {Guid.NewGuid():N}"[..30],
            Description = "test",
            StartPage = startPage
        };

        await using var db = _factory.NewDbContext();
        db.Roles.Add(role);
        db.RolePermissions.AddRange(rights.Select(r => new RolePermission { RoleId = role.Id, Permission = r }));
        await db.SaveChangesAsync();

        return role;
    }

    // ---------- Fetch by ID card ----------

    [Fact]
    public async Task A_voter_in_the_users_area_is_found_by_ID_card()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var user = await _factory.Data.UserAsync(["Collector"], [(constituency, null)]);

        var response = await (await _factory.ClientForAsync(user))
            .GetAsync($"api/Voters/by-national-id/{voter.NationalId.ToLowerInvariant()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var found = await response.Content.ReadFromJsonAsync<VoterResult>();
        Assert.Equal(voter.Id, found!.Id);
        Assert.False(found.HasPhoto);
    }

    [Fact]
    public async Task A_voter_outside_the_users_areas_looks_like_an_unknown_ID_card()
    {
        var mine = await _factory.Data.ConstituencyAsync();
        var other = await _factory.Data.ConstituencyAsync();
        var outsider = await _factory.Data.VoterAsync(other);
        var user = await _factory.Data.UserAsync(["Collector"], [(mine, null)]);

        var client = await _factory.ClientForAsync(user);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/Voters/by-national-id/{outsider.NationalId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("api/Voters/by-national-id/A000000")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("api/Voters/by-national-id/not-an-id")).StatusCode);
    }

    [Fact]
    public async Task Fetching_by_ID_card_needs_the_view_voters_right()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);
        var role = await CustomRoleAsync("Dashboard", "Encounters.Add");
        var user = await _factory.Data.UserAsync([role.Name], [(constituency, null)]);

        var response = await (await _factory.ClientForAsync(user)).GetAsync($"api/Voters/by-national-id/{voter.NationalId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_photo_flag_is_only_given_to_people_who_may_view_photos()
    {
        var constituency = await _factory.Data.ConstituencyAsync();
        var voter = await _factory.Data.VoterAsync(constituency);

        await using (var db = _factory.NewDbContext())
        {
            db.VoterPhotos.Add(new VoterPhoto
            {
                VoterId = voter.Id,
                Content = [0xFF, 0xD8, 0xFF, 0xD9],
                ContentType = "image/jpeg",
                SizeBytes = 4,
                UploadedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var without = await _factory.Data.UserAsync(["Collector"], [(constituency, null)]);
        var with = await _factory.Data.UserAsync(["Collector"], [(constituency, null)], extraRights: ["Voters.Photo.View"]);

        var a = await (await _factory.ClientForAsync(without)).GetFromJsonAsync<VoterResult>($"api/Voters/by-national-id/{voter.NationalId}");
        var b = await (await _factory.ClientForAsync(with)).GetFromJsonAsync<VoterResult>($"api/Voters/by-national-id/{voter.NationalId}");

        Assert.False(a!.HasPhoto);
        Assert.True(b!.HasPhoto);
    }

    // ---------- Start page ----------

    [Fact]
    public async Task A_role_set_to_quick_entry_skips_the_dashboard()
    {
        var role = await CustomRoleAsync("QuickEntry", "Voters.View", "Encounters.Add");
        var user = await _factory.Data.UserAsync([role.Name]);

        var me = await (await _factory.ClientForAsync(user)).GetFromJsonAsync<MyAccount>("api/Settings/me");

        Assert.Equal("QuickEntry", me!.StartPage);
    }

    [Fact]
    public async Task One_role_with_the_dashboard_is_enough_to_keep_it()
    {
        var quick = await CustomRoleAsync("QuickEntry", "Voters.View", "Encounters.Add");
        var user = await _factory.Data.UserAsync([quick.Name, "Reporter"]);

        var me = await (await _factory.ClientForAsync(user)).GetFromJsonAsync<MyAccount>("api/Settings/me");

        Assert.Equal("Dashboard", me!.StartPage);
    }

    [Fact]
    public async Task The_administrator_sets_a_roles_start_page_and_it_is_audited()
    {
        var role = await CustomRoleAsync("Dashboard", "Voters.View", "Pledges.Add");
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var client = await _factory.ClientForAsync(admin);

        var response = await client.PutAsJsonAsync($"api/Permissions/roles/{role.Id}/details",
            new { name = role.Name, description = "test", voterProfileView = "Full", startPage = "QuickEntry" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var db = _factory.NewDbContext();
        Assert.Equal("QuickEntry", (await db.Roles.SingleAsync(r => r.Id == role.Id)).StartPage);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == role.Name && a.Action.Contains("starts on")));
    }

    [Fact]
    public async Task The_administrator_role_always_starts_on_the_dashboard()
    {
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var client = await _factory.ClientForAsync(admin);

        await using var db = _factory.NewDbContext();
        var adminRole = await db.Roles.SingleAsync(r => r.Name == "Super Administrator");

        var response = await client.PutAsJsonAsync($"api/Permissions/roles/{adminRole.Id}/details",
            new { name = adminRole.Name, description = "", voterProfileView = "Full", startPage = "QuickEntry" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_the_administrator_can_change_a_roles_start_page()
    {
        var role = await CustomRoleAsync("Dashboard", "Voters.View");
        var national = await _factory.Data.UserAsync(["National Administrator"]);

        var response = await (await _factory.ClientForAsync(national)).PutAsJsonAsync($"api/Permissions/roles/{role.Id}/details",
            new { name = role.Name, description = "", voterProfileView = "Full", startPage = "QuickEntry" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
