using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Data.Seed;

public static class IslandSeeder
{
    public static async Task SeedAsync(HR28DbContext context)
    {
        if (await context.Islands.AnyAsync())
            return;

        var hulhumale = await context.Constituencies
            .FirstAsync(x => x.Name == "Hulhumale");

        var henveiru = await context.Constituencies
            .FirstAsync(x => x.Name == "Henveiru");

        // Each island belongs to exactly one constituency (Island.ConstituencyId);
        // the ConstituencyIsland rows below are kept in step until that table is dropped.
        var hulhumalePhase1 = new Island
        {
            Id = Guid.NewGuid(),
            Name = "Hulhumale Phase 1",
            ConstituencyId = hulhumale.Id
        };

        var hulhumalePhase2 = new Island
        {
            Id = Guid.NewGuid(),
            Name = "Hulhumale Phase 2",
            ConstituencyId = hulhumale.Id
        };

        var henveiruEast = new Island
        {
            Id = Guid.NewGuid(),
            Name = "Henveiru East",
            ConstituencyId = henveiru.Id
        };

        var henveiruWest = new Island
        {
            Id = Guid.NewGuid(),
            Name = "Henveiru West",
            ConstituencyId = henveiru.Id
        };

        var islands = new List<Island>
        {
            hulhumalePhase1,
            hulhumalePhase2,
            henveiruEast,
            henveiruWest
        };

        await context.Islands.AddRangeAsync(islands);

        var constituencyIslands = new List<ConstituencyIsland>
        {
            new()
            {
                ConstituencyId = hulhumale.Id,
                IslandId = hulhumalePhase1.Id
            },
            new()
            {
                ConstituencyId = hulhumale.Id,
                IslandId = hulhumalePhase2.Id
            },
            new()
            {
                ConstituencyId = henveiru.Id,
                IslandId = henveiruEast.Id
            },
            new()
            {
                ConstituencyId = henveiru.Id,
                IslandId = henveiruWest.Id
            }
        };

        await context.ConstituencyIslands.AddRangeAsync(
            constituencyIslands);

        await context.SaveChangesAsync();
    }
}