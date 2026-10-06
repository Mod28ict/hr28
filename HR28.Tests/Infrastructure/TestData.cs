using HR28.Application.Common;
using HR28.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests.Infrastructure;

/// <summary>
/// Creates synthetic records straight in the test database. Every call makes new, uniquely
/// named records, so tests sharing the database never see each other's data by accident.
/// </summary>
public sealed class TestData
{
    private static int _counter = 100_000;

    private readonly Hr28ApiFactory _factory;

    public TestData(Hr28ApiFactory factory) => _factory = factory;

    private static int Next() => Interlocked.Increment(ref _counter);

    /// <summary>A unique National ID in the HR28 format (letter + 6 digits).</summary>
    public static string NewNationalId() => $"Z{Next() % 1_000_000:D6}";

    /// <summary>A unique 7-digit mobile number.</summary>
    public static string NewMobile() => $"9{Next() % 1_000_000:D6}";

    public async Task<Constituency> ConstituencyAsync()
    {
        var n = Next();
        var constituency = new Constituency { Id = Guid.NewGuid(), Name = $"Test Constituency {n}", Code = $"T{n}" };

        await using var db = _factory.NewDbContext();
        db.Constituencies.Add(constituency);
        await db.SaveChangesAsync();

        return constituency;
    }

    /// <summary>An island in the constituency (with the matching link row, as IslandService keeps it).</summary>
    public async Task<Island> IslandAsync(Constituency constituency)
    {
        var island = new Island
        {
            Id = Guid.NewGuid(),
            Name = $"Test Island {Next()}",
            Atoll = "T",
            ConstituencyId = constituency.Id
        };

        await using var db = _factory.NewDbContext();
        db.Islands.Add(island);
        db.ConstituencyIslands.Add(new ConstituencyIsland { ConstituencyId = constituency.Id, IslandId = island.Id });
        await db.SaveChangesAsync();

        return island;
    }

    public async Task<Voter> VoterAsync(Constituency constituency, Island? island = null, string supportStatus = "Undecided")
    {
        var voter = new Voter
        {
            Id = Guid.NewGuid(),
            NationalId = NewNationalId(),
            FullName = $"Test Voter {Next()}",
            Address = $"Test House {Next()}",
            MobileNumber = NewMobile(),
            ConstituencyId = constituency.Id,
            IslandId = island?.Id,
            Remarks = string.Empty,
            CreatedAt = DateTime.UtcNow,
            SupportStatus = supportStatus,
            Gender = string.Empty,
            RegisteredIsland = string.Empty,
            AtollCode = string.Empty,
            ConstituencyCode = constituency.Code ?? string.Empty,
            ConstituencyName = constituency.Name
        };

        await using var db = _factory.NewDbContext();
        db.Voters.Add(voter);
        await db.SaveChangesAsync();

        return voter;
    }

    public async Task<Encounter> EncounterAsync(Voter voter, User recordedBy)
    {
        var encounter = new Encounter
        {
            Id = Guid.NewGuid(),
            VoterId = voter.Id,
            RecordedByUserId = recordedBy.Id,
            EncounterDate = MaldivesTime.Now.AddDays(-1),
            EncounterType = EncounterValues.Types[0],
            Outcome = EncounterValues.Outcomes[0],
            Response = EncounterValues.Responses[0],
            Notes = "Synthetic test note"
        };

        await using var db = _factory.NewDbContext();
        db.Encounters.Add(encounter);
        await db.SaveChangesAsync();

        return encounter;
    }

    public async Task<Pledge> PledgeAsync(Voter voter, User createdBy)
    {
        var pledge = new Pledge
        {
            Id = Guid.NewGuid(),
            VoterId = voter.Id,
            CreatedByUserId = createdBy.Id,
            PledgeDate = DateTime.UtcNow,
            Title = "Synthetic pledge",
            Description = "Test",
            Status = "Open",
            Priority = "Normal",
            ResolutionNotes = string.Empty
        };

        await using var db = _factory.NewDbContext();
        db.Pledges.Add(pledge);
        await db.SaveChangesAsync();

        return pledge;
    }

    /// <summary>
    /// A user with the given built-in or custom roles, areas and extra rights.
    /// An area is a whole constituency (island null) or one island in it.
    /// With an authorization code the user can also sign in through the API.
    /// </summary>
    public async Task<User> UserAsync(
        IEnumerable<string> roles,
        IEnumerable<(Constituency Constituency, Island? Island)>? areas = null,
        IEnumerable<string>? extraRights = null,
        string? authorizationCode = null,
        bool isActive = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            NationalId = NewNationalId(),
            FullName = $"Test User {Next()}",
            MobileNumber = NewMobile(),
            IsActive = isActive,
            AuthorizationCodeHash = authorizationCode == null ? null : _factory.HashAuthorizationCode(authorizationCode),
            CreatedAt = DateTime.UtcNow
        };

        await using var db = _factory.NewDbContext();
        db.Users.Add(user);

        foreach (var roleName in roles)
        {
            var role = await db.Roles.SingleAsync(r => r.Name == roleName);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        foreach (var (constituency, island) in areas ?? [])
        {
            db.UserScopes.Add(new UserScope
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ConstituencyId = constituency.Id,
                IslandId = island?.Id
            });
        }

        foreach (var right in extraRights ?? [])
            db.UserPermissions.Add(new UserPermission { UserId = user.Id, Permission = right });

        await db.SaveChangesAsync();

        return user;
    }

    /// <summary>A custom role with exactly these rights (no defaults).</summary>
    public async Task<string> CustomRoleAsync(params string[] rights)
    {
        var role = new Role { Id = Guid.NewGuid(), Name = $"Test Role {Next()}", Description = "Test" };

        await using var db = _factory.NewDbContext();
        db.Roles.Add(role);

        foreach (var right in rights)
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, Permission = right });

        await db.SaveChangesAsync();

        return role.Name;
    }

    /// <summary>Every right in the catalog (to show that rights alone never cross areas).</summary>
    public static string[] AllRights => PermissionCatalog.All.Select(p => p.Key).ToArray();
}
