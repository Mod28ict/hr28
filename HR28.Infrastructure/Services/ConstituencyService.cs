using HR28.Application.Common;
using HR28.Application.DTOs;
using HR28.Application.DTOs.Constituencies;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class ConstituencyService : IConstituencyService
{
    private readonly HR28DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ConstituencyService(
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

    public async Task<IEnumerable<ConstituencyDto>> GetAllAsync()
    {
        return await _context.Constituencies
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .ThenBy(c => c.Name)
            .Select(c => new ConstituencyDto
            {
                Id = c.Id,
                Code = c.Code ?? string.Empty,
                Name = c.Name,
                IslandCount = c.ConstituencyIslands.Count,
                VoterCount = _context.Voters.Count(v => v.ConstituencyId == c.Id)
            })
            .ToListAsync();
    }

    public async Task<ConstituencyDto?> GetByIdAsync(Guid id)
    {
        return await _context.Constituencies
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ConstituencyDto
            {
                Id = c.Id,
                Code = c.Code ?? string.Empty,
                Name = c.Name,
                IslandCount = c.ConstituencyIslands.Count
            })
            .FirstOrDefaultAsync();
    }

    public async Task<ConstituencyDto> CreateAsync(
        CreateConstituencyDto dto)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        var name = dto.Name.Trim();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException(
                "Constituency code and name are both required.");
        }

        var duplicateExists = await _context.Constituencies
            .AnyAsync(c =>
                (c.Code != null && c.Code == code) ||
                c.Name == name);

        if (duplicateExists)
        {
            throw new BusinessRuleException(
                "A constituency with the same code or name already exists.");
        }

        var constituency = new Constituency
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name
        };

        _context.Constituencies.Add(constituency);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(), "Create", "Constituency", constituency.Id.ToString());

        return new ConstituencyDto
        {
            Id = constituency.Id,
            Code = constituency.Code ?? string.Empty,
            Name = constituency.Name
        };
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateConstituencyDto dto)
    {
        var constituency = await _context.Constituencies
            .FirstOrDefaultAsync(c => c.Id == id);

        if (constituency is null)
            return false;

        var code = dto.Code.Trim().ToUpperInvariant();
        var name = dto.Name.Trim();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException(
                "Constituency code and name are both required.");
        }

        var duplicateExists = await _context.Constituencies
            .AnyAsync(c =>
                c.Id != id &&
                ((c.Code != null && c.Code == code) ||
                 c.Name == name));

        if (duplicateExists)
        {
            throw new BusinessRuleException(
                "Another constituency with the same code or name already exists.");
        }

        var changes = new List<string>();

        if (constituency.Code != code)
            changes.Add($"code \"{constituency.Code}\" → \"{code}\"");

        if (constituency.Name != name)
            changes.Add($"name \"{constituency.Name}\" → \"{name}\"");

        constituency.Code = code;
        constituency.Name = name;

        await _context.SaveChangesAsync();

        if (changes.Count > 0)
        {
            await _auditService.LogAsync(
                GetCurrentUserId(),
                "Update: " + string.Join(", ", changes),
                "Constituency",
                id.ToString());
        }

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var constituency = await _context.Constituencies
            .FirstOrDefaultAsync(c => c.Id == id);

        if (constituency is null)
            return false;

        var isInUse =
            await _context.Islands
                .AnyAsync(i => i.ConstituencyId == id) ||
            await _context.Set<ConstituencyIsland>()
                .AnyAsync(ci => ci.ConstituencyId == id);

        if (isInUse)
        {
            throw new BusinessRuleException(
                "The constituency cannot be deleted because it is linked to one or more islands.");
        }

        _context.Constituencies.Remove(constituency);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(), "Delete", "Constituency", id.ToString());

        return true;
    }

    public async Task<List<LookupDto>>
        GetIslandsByConstituencyAsync(
            Guid constituencyId)
    {
        return await _context.ConstituencyIslands
            .Where(ci => ci.ConstituencyId == constituencyId)
            .Select(ci => new LookupDto
            {
                Id = ci.Island.Id,
                Name = ci.Island.Name
            })
            .OrderBy(x => x.Name)
            .ToListAsync();
    }
}
