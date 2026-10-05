using HR28.Application.Common;
using HR28.Application.DTOs.Voters;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

/// <summary>
/// Political parties. Everyone signed in can read the list; the API lets only
/// administrators change it. Every change is audited.
/// </summary>
public class PoliticalPartyService : IPoliticalPartyService
{
    private const string AuditEntity = "Political party";

    private readonly HR28DbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PoliticalPartyService(
        HR28DbContext dbContext,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(
            _httpContextAccessor.HttpContext?.User
                .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var id)
            ? id
            : null;

    public async Task<List<PoliticalPartyDto>> GetAllAsync()
    {
        return await _dbContext.PoliticalParties
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .Select(p => new PoliticalPartyDto
            {
                Id = p.Id,
                Name = p.Name,
                ShortName = p.ShortName,
                SortOrder = p.SortOrder,
                IsDefaultFilter = p.IsDefaultFilter,
                VoterCount = _dbContext.Voters.Count(v => v.PoliticalPartyId == p.Id)
            })
            .ToListAsync();
    }

    private static string Clean(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private async Task<(string Name, string ShortName)> ValidateAsync(SavePoliticalPartyDto request, Guid? existingId)
    {
        var name = Clean(request.Name);
        var shortName = Clean(request.ShortName).ToUpperInvariant();

        if (name.Length == 0)
            throw new BusinessRuleException("Please enter the party's name.");

        if (name.Length > 100)
            throw new BusinessRuleException("Party names can be at most 100 characters.");

        if (shortName.Length == 0)
            throw new BusinessRuleException("Please enter a short name, e.g. MDP.");

        if (shortName.Length > 10)
            throw new BusinessRuleException("Short names can be at most 10 characters.");

        // SQL Server compares case-insensitively.
        var existing = await _dbContext.PoliticalParties
            .Where(p => (p.Name == name || p.ShortName == shortName) && p.Id != existingId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync();

        if (existing != null)
            throw new BusinessRuleException($"\"{existing}\" already has that name or short name.");

        return (name, shortName);
    }

    public async Task<PoliticalPartyDto> CreateAsync(SavePoliticalPartyDto request)
    {
        var (name, shortName) = await ValidateAsync(request, null);

        var nextOrder = (await _dbContext.PoliticalParties
            .MaxAsync(p => (int?)p.SortOrder) ?? 0) + 1;

        var party = new PoliticalParty
        {
            Id = Guid.NewGuid(),
            Name = name,
            ShortName = shortName,
            SortOrder = nextOrder,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.PoliticalParties.Add(party);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(CurrentUserId, $"Create ({shortName})", AuditEntity, name);

        return new PoliticalPartyDto { Id = party.Id, Name = name, ShortName = shortName, SortOrder = nextOrder };
    }

    public async Task<PoliticalPartyDto> UpdateAsync(Guid id, SavePoliticalPartyDto request)
    {
        var party = await _dbContext.PoliticalParties.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException("Party not found.");

        var (name, shortName) = await ValidateAsync(request, id);

        if (party.Name != name || party.ShortName != shortName)
        {
            var action = $"Rename: \"{party.Name} ({party.ShortName})\" → \"{name} ({shortName})\"";

            party.Name = name;
            party.ShortName = shortName;
            await _dbContext.SaveChangesAsync();

            await _auditService.LogAsync(CurrentUserId, action, AuditEntity, name);
        }

        return new PoliticalPartyDto
        {
            Id = id,
            Name = name,
            ShortName = shortName,
            SortOrder = party.SortOrder,
            IsDefaultFilter = party.IsDefaultFilter
        };
    }

    public async Task<string> DeleteAsync(Guid id)
    {
        var party = await _dbContext.PoliticalParties.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException("Party not found.");

        var inUse = await _dbContext.Voters.CountAsync(v => v.PoliticalPartyId == id);

        if (inUse > 0)
            throw new BusinessRuleException(
                $"{party.Name} is recorded for {inUse:N0} voter{(inUse == 1 ? "" : "s")}, so it can't be deleted. " +
                "You can rename it instead.");

        _dbContext.PoliticalParties.Remove(party);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(CurrentUserId, "Delete", AuditEntity, party.Name);

        return party.Name;
    }

    public async Task SetDefaultFilterAsync(Guid? id)
    {
        var parties = await _dbContext.PoliticalParties.ToListAsync();

        if (id.HasValue && parties.All(p => p.Id != id.Value))
            throw new KeyNotFoundException("Party not found.");

        var before = parties.FirstOrDefault(p => p.IsDefaultFilter)?.Name ?? "All parties";
        var after = parties.FirstOrDefault(p => p.Id == id)?.Name ?? "All parties";

        if (before == after)
            return;

        foreach (var party in parties)
            party.IsDefaultFilter = party.Id == id;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            CurrentUserId,
            $"Voters list opens on: \"{before}\" → \"{after}\"",
            AuditEntity,
            after);
    }
}
