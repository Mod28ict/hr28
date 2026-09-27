using HR28.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Data.Seed;

public static class IslandSeeder
{
    public static async Task SeedAsync(
        HR28DbContext context)
    {
        if (await context.Islands.AnyAsync())
            return;

        var hulhumale = await context.Constituencies
            .FirstAsync(x => x.Name == "Hulhumale");

        var henveiru = await context.Constituencies
            .FirstAsync(x => x.Name == "Henveiru");

        var islands = new List<Island>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Hulhumale Phase 1",
                ConstituencyId = hulhumale.Id
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Hulhumale Phase 2",
                ConstituencyId = hulhumale.Id
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Henveiru East",
                ConstituencyId = henveiru.Id
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Henveiru West",
                ConstituencyId = henveiru.Id
            }
        };

        await context.Islands.AddRangeAsync(islands);

        await context.SaveChangesAsync();
    }
}