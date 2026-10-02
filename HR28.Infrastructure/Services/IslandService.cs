using HR28.Application.Common;
using HR28.Application.DTOs.Islands;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class IslandService : IIslandService
{
    private readonly HR28DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IslandService(
        HR28DbContext context,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    private Guid? GetCurrentUserId()
    {
        var value = _httpContextAccessor.HttpContext?.User
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
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
                Atoll = i.Atoll,

                // The link table is what the island dropdowns read, so prefer it.
                ConstituencyId = i.ConstituencyIslands
                    .Select(ci => (Guid?)ci.ConstituencyId)
                    .FirstOrDefault() ?? i.ConstituencyId,
                ConstituencyName = i.ConstituencyIslands
                    .Select(ci => ci.Constituency.Name)
                    .FirstOrDefault() ?? i.Constituency.Name
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
                Atoll = i.Atoll,
                ConstituencyId = i.ConstituencyId,
                ConstituencyName = i.Constituency.Name
            })
            .FirstOrDefaultAsync();
    }

    private async Task ValidateAsync(Guid? islandId, string name, Guid constituencyId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Island name is required.");

        if (!await _context.Constituencies.AnyAsync(c => c.Id == constituencyId))
            throw new BusinessRuleException("Please choose a valid constituency.");

        var duplicate = await _context.Islands.AnyAsync(i =>
            i.Id != islandId &&
            i.Name == name &&
            (i.ConstituencyId == constituencyId ||
             i.ConstituencyIslands.Any(ci => ci.ConstituencyId == constituencyId)));

        if (duplicate)
            throw new BusinessRuleException("This constituency already has an island with that name.");
    }

    /// <summary>
    /// An island belongs to exactly one constituency: keep a single link row
    /// that matches Island.ConstituencyId.
    /// </summary>
    private async Task SyncConstituencyLinkAsync(Guid islandId, Guid constituencyId)
    {
        var links = await _context.ConstituencyIslands
            .Where(ci => ci.IslandId == islandId)
            .ToListAsync();

        _context.ConstituencyIslands.RemoveRange(
            links.Where(l => l.ConstituencyId != constituencyId));

        if (!links.Any(l => l.ConstituencyId == constituencyId))
        {
            _context.ConstituencyIslands.Add(new ConstituencyIsland
            {
                ConstituencyId = constituencyId,
                IslandId = islandId
            });
        }
    }

    public async Task<IslandDto> CreateAsync(CreateIslandDto dto)
    {
        var name = dto.Name.Trim();

        await ValidateAsync(null, name, dto.ConstituencyId);

        var island = new Island
        {
            Id = Guid.NewGuid(),
            Name = name,
            Atoll = dto.Atoll.Trim(),
            ConstituencyId = dto.ConstituencyId
        };

        _context.Islands.Add(island);

        await SyncConstituencyLinkAsync(island.Id, dto.ConstituencyId);

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(), "Create", "Island", island.Id.ToString());

        return (await GetByIdAsync(island.Id))!;
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateIslandDto dto)
    {
        var island = await _context.Islands
            .FirstOrDefaultAsync(i => i.Id == id);

        if (island == null)
            return false;

        var name = dto.Name.Trim();

        await ValidateAsync(id, name, dto.ConstituencyId);

        // Moving an island would orphan voters whose constituency no longer matches it.
        if (island.ConstituencyId != dto.ConstituencyId &&
            await _context.Voters.AnyAsync(v => v.IslandId == id))
        {
            throw new BusinessRuleException(
                "This island has voters, so it cannot be moved to another constituency.");
        }

        var changes = new List<string>();

        if (island.Name != name)
            changes.Add($"name \"{island.Name}\" → \"{name}\"");

        if (island.Atoll != dto.Atoll.Trim())
            changes.Add($"atoll \"{island.Atoll}\" → \"{dto.Atoll.Trim()}\"");

        if (island.ConstituencyId != dto.ConstituencyId)
            changes.Add("moved to another constituency");

        island.Name = name;
        island.Atoll = dto.Atoll.Trim();
        island.ConstituencyId = dto.ConstituencyId;

        await SyncConstituencyLinkAsync(id, dto.ConstituencyId);

        await _context.SaveChangesAsync();

        if (changes.Count > 0)
        {
            await _auditService.LogAsync(
                GetCurrentUserId(),
                "Update: " + string.Join(", ", changes),
                "Island",
                id.ToString());
        }

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var island = await _context.Islands
            .FirstOrDefaultAsync(i => i.Id == id);

        if (island == null)
            return false;

        if (await _context.Voters.AnyAsync(v => v.IslandId == id))
            throw new BusinessRuleException("This island has voters and cannot be deleted.");

        var links = _context.Set<ConstituencyIsland>()
            .Where(x => x.IslandId == id);

        _context.RemoveRange(links);

        _context.Islands.Remove(island);

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(), "Delete", "Island", id.ToString());

        return true;
    }
}
