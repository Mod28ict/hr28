using HR28.Application.Common;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using HR28.Infrastructure.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HR28.API.Maintenance;

/// <summary>
/// Server-only maintenance command, run on the server by its administrator:
///
///   dotnet HR28.API.dll reset-code A123456
///
/// Gives the user with that ID card a new authorization code and prints it once on the
/// console (it is never logged). Used to get the first Administrator in after a new
/// production key is set, when nobody can sign in yet. Like "Reset code" on the Users
/// page: old code, open SMS codes and remembered devices stop working; audited.
/// The web app and API keep running; nothing else is changed.
/// </summary>
public static class ResetCodeCommand
{
    public const string Name = "reset-code";

    public static async Task<int> RunAsync(IServiceProvider services, string nationalId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HR28DbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IAuthorizationCodeHasher>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var nid = MaldivesFormats.CleanNationalId(nationalId);
        var user = await db.Users.FirstOrDefaultAsync(u => u.NationalId == nid);

        if (user == null)
        {
            Console.Error.WriteLine($"No user has ID card {nid}.");
            return 1;
        }

        string code, hash;

        do
        {
            code = AuthorizationCodeGenerator.Generate();
            hash = hasher.Hash(code);
        }
        while (await db.Users.AnyAsync(u => u.AuthorizationCodeHash == hash));

        var now = DateTime.UtcNow;

        await db.AuthorizationCodeHistories
            .Where(h => h.UserId == user.Id && h.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.RevokedAt, now));

        user.AuthorizationCodeHash = hash;
        user.AuthorizationCode = null;
        user.FailedLoginAttempts = 0;
        user.LockedUntilUtc = null;

        db.AuthorizationCodeHistories.Add(new AuthorizationCodeHistory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AuthorizationCodeHash = hash,
            CreatedAt = now,
            Reason = "Reset on the server by its administrator"
        });

        await db.OtpRequests.Where(o => o.UserId == user.Id && !o.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true));

        await db.TrustedDevices.Where(d => d.UserId == user.Id).ExecuteDeleteAsync();

        await db.SaveChangesAsync();

        await audit.LogAsync(null, "Authorization code reset on the server", "User", user.Id.ToString());

        Console.WriteLine($"New authorization code for {user.FullName} ({nid}): {code}");
        Console.WriteLine(user.IsActive
            ? "Give it to them in person. It is not shown again."
            : "The account is inactive: activate it on the Users page after signing in as an Administrator.");

        return 0;
    }
}
