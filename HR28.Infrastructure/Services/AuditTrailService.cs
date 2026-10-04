using HR28.Application.Common;
using HR28.Application.DTOs.Audit;
using HR28.Application.DTOs.Common;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class AuditTrailService : IAuditTrailService
{
    private readonly HR28DbContext _dbContext;
    private readonly IAccessScopeService _accessScopeService;

    public AuditTrailService(
        HR28DbContext dbContext,
        IAccessScopeService accessScopeService)
    {
        _dbContext = dbContext;
        _accessScopeService = accessScopeService;
    }

    private async Task<IQueryable<AuditLog>> VisibleLogsAsync(Guid userId)
    {
        var scope = await _accessScopeService.GetAsync(userId);

        var logs = _dbContext.AuditLogs.AsNoTracking();

        return scope.IsAdministrator
            ? logs
            : logs.Where(a => a.UserId == userId);
    }

    public async Task<PagedResult<AuditEntryDto>> GetPagedAsync(
        Guid userId,
        AuditQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 10, 100);

        var logs = await VisibleLogsAsync(userId);

        if (!string.IsNullOrWhiteSpace(query.EntityName))
            logs = logs.Where(a => a.EntityName == query.EntityName);

        if (!string.IsNullOrWhiteSpace(query.Action))
            logs = logs.Where(a => a.Action == query.Action);

        if (query.From.HasValue)
        {
            // From/To are Maldives dates; entries are stored in UTC.
            var fromUtc = MaldivesTime.ToUtc(query.From.Value.Date);
            logs = logs.Where(a => a.CreatedAt >= fromUtc);
        }

        if (query.To.HasValue)
        {
            var toUtc = MaldivesTime.ToUtc(query.To.Value.Date.AddDays(1));
            logs = logs.Where(a => a.CreatedAt < toUtc);
        }

        var rows =
            from a in logs
            join u in _dbContext.Users on a.UserId equals (Guid?)u.Id into actors
            from u in actors.DefaultIfEmpty()
            select new { Log = a, ActorName = u != null ? u.FullName : null };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();

            rows = rows.Where(r =>
                r.Log.Action.Contains(term) ||
                r.Log.EntityName.Contains(term) ||
                (r.ActorName != null && r.ActorName.Contains(term)));
        }

        var totalCount = await rows.CountAsync();

        var items = await rows
            .OrderByDescending(r => r.Log.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AuditEntryDto
            {
                Id = r.Log.Id,
                CreatedAt = r.Log.CreatedAt,
                Action = r.Log.Action,
                EntityName = r.Log.EntityName,
                EntityId = r.Log.EntityId,
                ActorId = r.Log.UserId,
                ActorName = r.ActorName ?? "System"
            })
            .ToListAsync();

        await AddEntityLabelsAsync(items);

        return new PagedResult<AuditEntryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<List<AuditEntryDto>> GetRecentAsync(Guid userId, int count)
    {
        var result = await GetPagedAsync(userId, new AuditQueryDto
        {
            Page = 1,
            PageSize = Math.Clamp(count, 10, 100)
        });

        return result.Items.Take(count).ToList();
    }

    public async Task<AuditFacetsDto> GetFacetsAsync(Guid userId)
    {
        var scope = await _accessScopeService.GetAsync(userId);
        var logs = await VisibleLogsAsync(userId);

        return new AuditFacetsDto
        {
            IsAdministrator = scope.IsAdministrator,
            EntityNames = await logs.Select(a => a.EntityName).Distinct().OrderBy(x => x).ToListAsync(),
            Actions = await logs.Select(a => a.Action).Distinct().OrderBy(x => x).ToListAsync()
        };
    }

    /// <summary>
    /// Replaces record ids with readable names (one query per record type on the page).
    /// </summary>
    private async Task AddEntityLabelsAsync(List<AuditEntryDto> items)
    {
        Guid[] IdsFor(string entityName) => items
            .Where(i => i.EntityName == entityName)
            .Select(i => Guid.TryParse(i.EntityId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void AddAll(string entityName, IEnumerable<(Guid Id, string Label)> found)
        {
            foreach (var (id, label) in found)
                labels[$"{entityName}:{id}"] = label;
        }

        var voterIds = IdsFor("Voter");
        if (voterIds.Length > 0)
        {
            AddAll("Voter", (await _dbContext.Voters
                    .Where(v => voterIds.Contains(v.Id))
                    .Select(v => new { v.Id, v.FullName })
                    .ToListAsync())
                .Select(x => (x.Id, x.FullName)));
        }

        var influencerIds = IdsFor("Influencer");
        if (influencerIds.Length > 0)
        {
            AddAll("Influencer", (await _dbContext.Influencers
                    .Where(i => influencerIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.FullName })
                    .ToListAsync())
                .Select(x => (x.Id, x.FullName)));
        }

        var userIds = IdsFor("User");
        if (userIds.Length > 0)
        {
            AddAll("User", (await _dbContext.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FullName })
                    .ToListAsync())
                .Select(x => (x.Id, x.FullName)));
        }

        var pledgeIds = IdsFor("Pledge");
        if (pledgeIds.Length > 0)
        {
            AddAll("Pledge", (await _dbContext.Pledges
                    .Where(p => pledgeIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.Title, VoterName = p.Voter.FullName })
                    .ToListAsync())
                .Select(x => (x.Id, $"{x.Title} — {x.VoterName}")));
        }

        var encounterIds = IdsFor("Encounter");
        if (encounterIds.Length > 0)
        {
            AddAll("Encounter", (await _dbContext.Encounters
                    .Where(e => encounterIds.Contains(e.Id))
                    .Select(e => new { e.Id, e.EncounterType, VoterName = e.Voter.FullName })
                    .ToListAsync())
                .Select(x => (x.Id, $"{x.EncounterType} — {x.VoterName}")));
        }

        var constituencyIds = IdsFor("Constituency");
        if (constituencyIds.Length > 0)
        {
            AddAll("Constituency", (await _dbContext.Constituencies
                    .Where(c => constituencyIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name })
                    .ToListAsync())
                .Select(x => (x.Id, x.Name)));
        }

        var islandIds = IdsFor("Island");
        if (islandIds.Length > 0)
        {
            AddAll("Island", (await _dbContext.Islands
                    .Where(i => islandIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.Name })
                    .ToListAsync())
                .Select(x => (x.Id, x.Name)));
        }

        foreach (var item in items)
        {
            if (Guid.TryParse(item.EntityId, out var id) &&
                labels.TryGetValue($"{item.EntityName}:{id}", out var label))
            {
                item.EntityLabel = label;
            }
            else if (!Guid.TryParse(item.EntityId, out _) && !string.IsNullOrWhiteSpace(item.EntityId))
            {
                // Non-id keys (e.g. a setting name) are already readable.
                item.EntityLabel = item.EntityId;
            }
        }
    }
}
