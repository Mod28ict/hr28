using System.Net;
using System.Net.Http.Json;
using HR28.API.Maintenance;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>Server checks and the server-only "reset-code" command.</summary>
[Collection(ApiCollection.Name)]
public class ServerMaintenanceTests
{
    private readonly Hr28ApiFactory _factory;

    public ServerMaintenanceTests(Hr28ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_says_ok_when_the_database_is_reachable()
    {
        var response = await _factory.AnonymousClient().GetAsync("health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reset_code_gives_a_working_code_and_stops_the_old_one()
    {
        var user = await _factory.Data.UserAsync(["Super Administrator"], authorizationCode: "OLDC0DE9");

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exit;
        try
        {
            exit = await ResetCodeCommand.RunAsync(_factory.Services, user.NationalId.ToLowerInvariant());
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal(0, exit);
        var code = System.Text.RegularExpressions.Regex.Match(output.ToString(), @"[A-Z2-9]{4}-[A-Z2-9]{4}").Value;
        Assert.False(string.IsNullOrEmpty(code));

        var client = _factory.AnonymousClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("api/Auth/generate-otp", new { authorizationCode = code })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _factory.AnonymousClient().PostAsJsonAsync("api/Auth/generate-otp", new { authorizationCode = "OLDC0DE9" })).StatusCode);

        await using var db = _factory.NewDbContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == user.Id.ToString() && a.Action == "Authorization code reset on the server"));
    }

    [Fact]
    public async Task Reset_code_for_an_unknown_ID_card_changes_nothing()
    {
        var error = new StringWriter();
        var original = Console.Error;
        Console.SetError(error);
        try
        {
            Assert.Equal(1, await ResetCodeCommand.RunAsync(_factory.Services, "A000001"));
        }
        finally
        {
            Console.SetError(original);
        }

        Assert.Contains("No user has ID card", error.ToString());
    }
}
