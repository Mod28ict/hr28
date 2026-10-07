using System.Net;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;

namespace HR28.Tests;

/// <summary>Reports are rights: "View reports" and "Download and print reports".</summary>
[Collection(ApiCollection.Name)]
public class ReportRightsTests
{
    private readonly Hr28ApiFactory _factory;

    public ReportRightsTests(Hr28ApiFactory factory) => _factory = factory;

    private async Task<string> RoleWithAsync(params string[] rights)
    {
        var role = new Role { Id = Guid.NewGuid(), Name = $"Reports {Guid.NewGuid():N}"[..30], Description = "test" };

        await using var db = _factory.NewDbContext();
        db.Roles.Add(role);
        db.RolePermissions.AddRange(rights.Select(r => new RolePermission { RoleId = role.Id, Permission = r }));
        await db.SaveChangesAsync();

        return role.Name;
    }

    private static readonly string[] Pages =
        ["api/Reports/constituency-summary", "api/Reports/pledge-status-summary", "api/Reports/top-influencers"];

    private static readonly string[] Downloads =
        ["api/Reports/constituency-summary/export", "api/Reports/pledge-status-summary/export", "api/Reports/top-influencers/export"];

    [Fact]
    public async Task Without_the_rights_reports_are_refused()
    {
        var user = await _factory.Data.UserAsync([await RoleWithAsync("Voters.View")]);
        var client = await _factory.ClientForAsync(user);

        foreach (var url in Pages.Concat(Downloads))
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task View_reports_opens_them_but_does_not_download()
    {
        var user = await _factory.Data.UserAsync([await RoleWithAsync("Reports.View")]);
        var client = await _factory.ClientForAsync(user);

        foreach (var url in Pages)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);

        foreach (var url in Downloads)
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Download_right_allows_the_CSV_downloads()
    {
        var user = await _factory.Data.UserAsync([await RoleWithAsync("Reports.View", "Reports.Download")]);
        var client = await _factory.ClientForAsync(user);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Downloads[0])).StatusCode);
    }

    [Fact]
    public async Task Built_in_Reporter_role_has_both_report_rights()
    {
        var reporter = await _factory.Data.UserAsync(["Reporter"]);
        var client = await _factory.ClientForAsync(reporter);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Pages[0])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Downloads[0])).StatusCode);
    }
}
