using HR28.Application.DTOs.Islands;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace HR28.Infrastructure.Services;

public class IslandService : IIslandService
{
    private readonly HR28DbContext _context;

    public IslandService(HR28DbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<IslandDto>> GetAllAsync()
    {
        return await _context.Islands
            .AsNoTracking()
            .OrderBy(i => i.Name)
            .Select(i => new IslandDto
            {
                Id = i.Id,
                Name = i.Name,
                Atoll = i.Atoll
            })
            .ToListAsync();
    }

    public async Task<IslandDto?> GetByIdAsync(Guid id)
    {
        return await _context.Islands
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new IslandDto
            {
                Id = i.Id,
                Name = i.Name,
                Atoll = i.Atoll
            })
            .FirstOrDefaultAsync();
    }

    public async Task<IslandDto> CreateAsync(CreateIslandDto dto)
    {
        var island = new Island
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Atoll = dto.Atoll.Trim(),
            ConstituencyId = dto.ConstituencyId
        };

        _context.Islands.Add(island);

        await _context.SaveChangesAsync();

        return new IslandDto
        {
            Id = island.Id,
            Name = island.Name
        };
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateIslandDto dto)
    {
        var island = await _context.Islands
            .FirstOrDefaultAsync(i => i.Id == id);

        if (island == null)
            return false;

        island.Name = dto.Name.Trim();
        island.Atoll = dto.Atoll.Trim();
        island.ConstituencyId = dto.ConstituencyId;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var island = await _context.Islands
            .FirstOrDefaultAsync(i => i.Id == id);

        if (island == null)
            return false;
        var links = _context.Set<ConstituencyIsland>()
    .Where(x => x.IslandId == id);

        _context.RemoveRange(links);

        _context.Islands.Remove(island);

        await _context.SaveChangesAsync();

        return true;
    }
}