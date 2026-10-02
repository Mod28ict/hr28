using HR28.Application.DTOs.Access;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class AccessScopeService : IAccessScopeService
{
    // Roles that see every record regardless of assigned scope.
    public static readonly string[] AdministratorRoles =
    {
        "Super Administrator",
        "National Administrator"
    };

    private readonly HR28DbContext _dbContext;

    public AccessScopeService(HR28DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AccessScope> GetAsync(Guid userId)
    {
        var isAdministrator = await _dbContext.UserRoles
            .AnyAsync(ur =>
                ur.UserId == userId &&
                AdministratorRoles.Contains(ur.Role.Name));

        if (isAdministrator)
        {
            return new AccessScope
            {
                UserId = userId,
                IsAdministrator = true
            };
        }

        var scopes = await _dbContext.UserScopes
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.ConstituencyId, x.IslandId })
            .ToListAsync();

        var constituencyIds = scopes
            .Where(x => x.ConstituencyId.HasValue && !x.IslandId.HasValue)
            .Select(x => x.ConstituencyId!.Value)
            .Distinct()
            .ToList();

        var islandIds = scopes
            .Where(x => x.IslandId.HasValue)
            .Select(x => x.IslandId!.Value)
            .Distinct()
            .ToList();

        var visibleConstituencyIds = scopes
            .Where(x => x.ConstituencyId.HasValue)
            .Select(x => x.ConstituencyId!.Value)
            .Distinct()
            .ToList();

        return new AccessScope
        {
            UserId = userId,
            IsAdministrator = false,
            ConstituencyIds = constituencyIds,
            IslandIds = islandIds,
            VisibleConstituencyIds = visibleConstituencyIds
        };
    }
}

/// <summary>
/// Applies an <see cref="AccessScope"/> to queries so filtering happens in SQL.
/// </summary>
public static class ScopeQueryExtensions
{
    /// <summary>
    /// Throws KeyNotFoundException (→ 404) if the voter is missing or outside the scope,
    /// so out-of-scope records look the same as records that don't exist.
    /// </summary>
    public static async Task EnsureVoterInScopeAsync(
        this HR28DbContext dbContext,
        AccessScope scope,
        Guid voterId)
    {
        var visible = await dbContext.Voters
            .InScope(scope)
            .AnyAsync(v => v.Id == voterId);

        if (!visible)
            throw new KeyNotFoundException("Voter not found.");
    }

    public static IQueryable<Voter> InScope(
        this IQueryable<Voter> query,
        AccessScope scope)
    {
        if (scope.IsAdministrator)
            return query;

        if (!scope.HasAnyScope)
            return query.Where(_ => false);

        var constituencyIds = scope.ConstituencyIds.ToList();
        var islandIds = scope.IslandIds.ToList();

        return query.Where(v =>
            constituencyIds.Contains(v.ConstituencyId) ||
            (v.IslandId.HasValue && islandIds.Contains(v.IslandId.Value)));
    }

    public static IQueryable<Influencer> InScope(
        this IQueryable<Influencer> query,
        AccessScope scope)
    {
        if (scope.IsAdministrator)
            return query;

        if (!scope.HasAnyScope)
            return query.Where(_ => false);

        var constituencyIds = scope.ConstituencyIds.ToList();
        var islandIds = scope.IslandIds.ToList();

        return query.Where(i =>
            constituencyIds.Contains(i.ConstituencyId) ||
            (i.IslandId.HasValue && islandIds.Contains(i.IslandId.Value)));
    }

    public static IQueryable<Encounter> InScope(
        this IQueryable<Encounter> query,
        AccessScope scope,
        IQueryable<Voter> voters)
    {
        if (scope.IsAdministrator)
            return query;

        var voterIds = voters.InScope(scope).Select(v => v.Id);

        return query.Where(e => voterIds.Contains(e.VoterId));
    }

    public static IQueryable<Pledge> InScope(
        this IQueryable<Pledge> query,
        AccessScope scope,
        IQueryable<Voter> voters)
    {
        if (scope.IsAdministrator)
            return query;

        var voterIds = voters.InScope(scope).Select(v => v.Id);

        return query.Where(p => voterIds.Contains(p.VoterId));
    }

    public static IQueryable<Constituency> InScope(
        this IQueryable<Constituency> query,
        AccessScope scope)
    {
        if (scope.IsAdministrator)
            return query;

        var ids = scope.VisibleConstituencyIds.ToList();

        return query.Where(c => ids.Contains(c.Id));
    }
}
