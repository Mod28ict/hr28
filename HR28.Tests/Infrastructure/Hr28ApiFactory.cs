using System.Net;
using System.Net.Http.Headers;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HR28.Tests.Infrastructure;

/// <summary>Every API test class joins this collection and shares one app and one test database.</summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<Hr28ApiFactory>
{
    public const string Name = "HR28 API";
}

/// <summary>
/// Runs the real HR28 API in memory against a throwaway SQL Server database, built from the
/// migrations and deleted when the tests finish. It never touches HR28Db or a client database.
///
/// Server: the HR28_TEST_SQL environment variable (a connection string without a database
/// name), or the local SQL Server with Windows sign-in, which is what Visual Studio on the
/// owner's PC uses. The database name is always HR28_Tests_&lt;random&gt;.
/// </summary>
public sealed class Hr28ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Test-only keys, used nowhere else.
    public const string JwtKey = "hr28-tests-only-jwt-signing-key-0123456789";
    public static readonly string AuthorizationCodeKey =
        Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    /// <summary>Sets the caller's address for one request (sign-in limits are counted per address).</summary>
    public const string ClientIpHeader = "X-Test-Client-IP";

    private static int _nextIp;

    public Hr28ApiFactory()
    {
        var server = Environment.GetEnvironmentVariable("HR28_TEST_SQL")
            ?? "Server=localhost;Integrated Security=True;TrustServerCertificate=True";

        ConnectionString = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = $"HR28_Tests_{Guid.NewGuid():N}",
            MultipleActiveResultSets = true
        }.ConnectionString;

        Data = new TestData(this);
    }

    public string ConnectionString { get; }

    /// <summary>Captures every SMS instead of sending it.</summary>
    public FakeSmsSender Sms { get; } = new();

    public TestData Data { get; }

    public async Task InitializeAsync()
    {
        await using (var db = NewDbContext())
            await db.Database.MigrateAsync();

        // Start the app now (its startup seeds the built-in roles).
        _ = Server;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        SqlConnection.ClearAllPools();

        await using var db = NewDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: no readable SMS codes, no Swagger, no development SMS sender.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Security:AuthorizationCodeKey", AuthorizationCodeKey);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISmsSender>();
            services.AddSingleton<ISmsSender>(Sms);
            services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>();
        });
    }

    public HR28DbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<HR28DbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>A fresh, unused caller address, so tests don't share sign-in limits.</summary>
    public static string NextClientIp()
    {
        var n = Interlocked.Increment(ref _nextIp);
        return $"10.{(n >> 16) & 255}.{(n >> 8) & 255}.{n & 255}";
    }

    /// <summary>A client that is not signed in, calling from its own address.</summary>
    public HttpClient AnonymousClient(string? clientIp = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ClientIpHeader, clientIp ?? NextClientIp());
        return client;
    }

    /// <summary>A client signed in as the user, with a token issued exactly as sign-in issues it.</summary>
    public async Task<HttpClient> ClientForAsync(User user)
    {
        var client = AnonymousClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TokenForAsync(user));
        return client;
    }

    public async Task<string> TokenForAsync(User user)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateTokenAsync(user);
    }

    public string HashAuthorizationCode(string code) =>
        Services.GetRequiredService<IAuthorizationCodeHasher>().Hash(code);

    /// <summary>Applies the X-Test-Client-IP header before anything else in the pipeline.</summary>
    private sealed class TestClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(ClientIpHeader, out var value) &&
                    IPAddress.TryParse(value, out var ip))
                {
                    context.Connection.RemoteIpAddress = ip;
                }

                return nextMiddleware(context);
            });

            next(app);
        };
    }
}
