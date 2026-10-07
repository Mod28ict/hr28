using HR28.Domain.Entities;

namespace HR28.Infrastructure.Data.Seed;

public static class RoleSeeder
{
    public static async Task SeedRolesAsync(HR28DbContext context)
    {
        if (context.Roles.Any())
            return;

        var roles = new List<Role>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Super Administrator",
                Description = "Full system access"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "National Administrator",
                Description = "National level access"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Constituency Administrator",
                Description = "Constituency level access"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Island Administrator",
                Description = "Island level access"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Collector",
                Description = "Data collection access"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Reporter",
                Description = "Reporting access"
            }
        };

        await context.Roles.AddRangeAsync(roles);

        // A new database runs every migration before these roles exist, so the defaults
        // that migration AddRoleRights grants to existing roles must be granted here too
        // (same values), or a new client's built-in roles would start with no rights.
        foreach (var role in roles)
        {
            if (!DefaultRights.TryGetValue(role.Name, out var rights))
                continue;

            foreach (var right in rights)
                context.RolePermissions.Add(new RolePermission { RoleId = role.Id, Permission = right });
        }

        await context.SaveChangesAsync();
    }

    private static readonly string[] AdminAndCollectorRights =
    {
        "Voters.View", "Voters.Add", "Voters.Edit",
        // Given with "Edit voters" (migration GrantVoterStatusToEditors).
        "Voters.Status",
        "Encounters.View", "Encounters.Add",
        "Pledges.View", "Pledges.Add", "Pledges.Edit",
        "Influencers.View", "Influencers.Add", "Influencers.Link",
        // Migration GrantReportRights.
        "Reports.View", "Reports.Download"
    };

    /// <summary>
    /// Built-in roles' rights on a new database: the same as migrations AddRoleRights,
    /// GrantVoterStatusToEditors and GrantReportRights.
    /// The Super Administrator needs none (it always has every right).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> DefaultRights =
        new Dictionary<string, string[]>
        {
            ["National Administrator"] = [.. AdminAndCollectorRights, "Voters.Delete"],
            ["Constituency Administrator"] = AdminAndCollectorRights,
            ["Island Administrator"] = AdminAndCollectorRights,
            ["Collector"] = AdminAndCollectorRights,
            ["Reporter"] = ["Encounters.View", "Pledges.View", "Reports.View", "Reports.Download"]
        };
}