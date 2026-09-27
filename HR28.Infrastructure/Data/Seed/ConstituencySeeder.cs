using HR28.Domain.Entities;

namespace HR28.Infrastructure.Data.Seed;

public static class ConstituencySeeder
{
    public static async Task SeedAsync(
        HR28DbContext context)
    {
        if (context.Constituencies.Any())
            return;

        var constituencies = new List<Constituency>
        {
            new() { Id = Guid.NewGuid(), Name = "Henveiru" },
            new() { Id = Guid.NewGuid(), Name = "Galolhu" },
            new() { Id = Guid.NewGuid(), Name = "Maafannu" },
            new() { Id = Guid.NewGuid(), Name = "Machangolhi" },
            new() { Id = Guid.NewGuid(), Name = "Hulhumale" }
        };

        await context.Constituencies.AddRangeAsync(
            constituencies);

        await context.SaveChangesAsync();
    }
}