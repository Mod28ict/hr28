using System.Net;
using System.Net.Http.Headers;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HR28.Tests;

/// <summary>"End all sessions": the administrator signs a person out everywhere at once.</summary>
[Collection(ApiCollection.Name)]
public class EndSessionsTests
{
    private readonly Hr28ApiFactory _factory;

    public EndSessionsTests(Hr28ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClientSignedInAtAsync(User user, DateTime signedInAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var token = await scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateTokenAsync(user, signedInAtUtc);

        var client = _factory.AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Ending_sessions_refuses_every_earlier_sign_in_but_not_a_new_one()
    {
        var person = await _factory.Data.UserAsync(["Collector"]);
        var laptop = await ClientSignedInAtAsync(person, DateTime.UtcNow.AddMinutes(-30));
        var phone = await ClientSignedInAtAsync(person, DateTime.UtcNow.AddMinutes(-2));

        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("api/Settings/me")).StatusCode);

        var admin = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Super Administrator"]));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"api/Users/{person.Id}/end-sessions", null)).StatusCode);

        // Both earlier sign-ins are refused, including staying signed in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("api/Settings/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("api/Settings/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.PostAsync("api/Auth/refresh", null)).StatusCode);

        // Signing in again works.
        await Task.Delay(1100);
        var again = await ClientSignedInAtAsync(person, DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("api/Settings/me")).StatusCode);

        await using var db = _factory.NewDbContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == person.Id.ToString() && a.Action == "Ended all sessions"));
    }

    [Fact]
    public async Task The_administrator_cannot_end_their_own_sessions_here()
    {
        var admin = await _factory.Data.UserAsync(["Super Administrator"]);
        var client = await _factory.ClientForAsync(admin);

        var response = await client.PostAsync($"api/Users/{admin.Id}/end-sessions", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_the_administrator_can_end_sessions()
    {
        var person = await _factory.Data.UserAsync(["Collector"]);
        var national = await _factory.ClientForAsync(await _factory.Data.UserAsync(["National Administrator"]));

        var response = await national.PostAsync($"api/Users/{person.Id}/end-sessions", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
