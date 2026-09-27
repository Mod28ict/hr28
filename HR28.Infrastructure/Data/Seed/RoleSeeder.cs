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

        await context.SaveChangesAsync();
    }
}