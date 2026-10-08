using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HR28.Tests;

/// <summary>
/// Staying signed in: an active session gets a fresh 1-hour token (keeping the original
/// sign-in time); never past 12 hours from sign-in, and never for a deactivated account.
/// </summary>
[Collection(ApiCollection.Name)]
public class SessionRefreshTests
{
    private readonly Hr28ApiFactory _factory;

    public SessionRefreshTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record Refreshed(string Token);

    private async Task<HttpClient> ClientSignedInAtAsync(User user, DateTime signedInAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var token = await scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateTokenAsync(user, signedInAtUtc);

        var client = _factory.AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string SignedInAt(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.First(c => c.Type == ITokenService.SignedInAtClaim).Value;

    [Fact]
    public async Task An_active_session_gets_a_fresh_one_hour_token_with_the_same_sign_in_time()
    {
        var user = await _factory.Data.UserAsync(["Collector"]);
        var signedIn = DateTime.UtcNow.AddMinutes(-50);
        var client = await ClientSignedInAtAsync(user, signedIn);

        var response = await client.PostAsync("api/Auth/refresh", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<Refreshed>())!.Token;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.InRange(jwt.ValidTo, DateTime.UtcNow.AddMinutes(55), DateTime.UtcNow.AddMinutes(61));
        Assert.Equal(new DateTimeOffset(signedIn).ToUnixTimeSeconds().ToString(), SignedInAt(token));
    }

    [Fact]
    public async Task After_12_hours_from_sign_in_a_new_sign_in_is_needed()
    {
        var user = await _factory.Data.UserAsync(["Collector"]);
        var client = await ClientSignedInAtAsync(user, DateTime.UtcNow.AddHours(-12).AddMinutes(-1));

        var response = await client.PostAsync("api/Auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_account_cannot_stay_signed_in()
    {
        var user = await _factory.Data.UserAsync(["Collector"]);
        var client = await ClientSignedInAtAsync(user, DateTime.UtcNow.AddMinutes(-5));

        await using (var db = _factory.NewDbContext())
            await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

        var response = await client.PostAsync("api/Auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refreshing_needs_a_signed_in_user()
    {
        var response = await _factory.AnonymousClient().PostAsync("api/Auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
