using HR28.Application.Common;
using HR28.Application.DTOs.Access;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HR28.Infrastructure.Services;

public class AccessScopeService : IAccessScopeService
{
    // Roles that see every record regardless of assigned scope.
    public static readonly string[] AdministratorRoles =
    {
        "Super Administrator",
        "National Administrator"
    };

    public const string SuperAdministratorRole = "Super Administrator";

    // Short enough that a change made on another server instance shows up quickly;
    // on this instance Invalidate() makes it immediate.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly HR28DbContext _dbContext;
    private readonly IMemoryCache _cache;

    public AccessScopeService(HR28DbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    // Bumped by InvalidateAll(); part of every cache key, so old entries are simply never read again.
    private static long _generation;

    private static string CacheKey(Guid userId) =>
        $"access-scope:{Interlocked.Read(ref _generation)}:{userId}";

    public void Invalidate(Guid userId) => _cache.Remove(CacheKey(userId));

    public void InvalidateAll() => Interlocked.Increment(ref _generation);

    public async Task<AccessScope> GetAsync(Guid userId)
    {
        if (_cache.TryGetValue(CacheKey(userId), out AccessScope? cached) && cached != null)
            return cached;

        var scope = await LoadAsync(userId);

        _cache.Set(CacheKey(userId), scope, CacheDuration);

        return scope;
    }

    private async Task<AccessScope> LoadAsync(Guid userId)
    {
        var account = await _dbContext.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { u.SessionsEndedAt })
            .FirstOrDefaultAsync();

        // A deactivated or deleted account gets no roles and no records.
        if (account == null)
            return new AccessScope { UserId = userId, IsActive = false };

        var sessionsEndedAt = account.SessionsEndedAt;

        var roleRows = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => new { ur.Role.Name, ur.Role.VoterProfileView, ur.Role.StartPage })
            .ToListAsync();

        var roles = roleRows.Select(r => r.Name).Distinct().ToList();

        var isAdministrator = roles.Any(r => AdministratorRoles.Contains(r));

        // The fullest view wins: one role with the full profile is enough.
        var profileView =
            roles.Contains(SuperAdministratorRole) ||
            roleRows.Count == 0 ||
            roleRows.Any(r => r.VoterProfileView != VoterProfileViews.AddEncounter)
                ? VoterProfileViews.Full
                : VoterProfileViews.AddEncounter;

        // Same rule for the start page: one role with the Dashboard is enough.
        var startPage =
            roles.Contains(SuperAdministratorRole) ||
            roleRows.Count == 0 ||
            roleRows.Any(r => r.StartPage != StartPages.QuickEntry)
                ? StartPages.Dashboard
                : StartPages.QuickEntry;

        // Rights = those of every role the user has + those granted to the user.
        var rolePermissions = await _dbContext.RolePermissions
            .Where(rp => _dbContext.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == rp.RoleId))
            .Select(rp => rp.Permission)
            .ToListAsync();

        var userPermissions = await _dbContext.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission)
            .ToListAsync();

        var permissions = rolePermissions
            .Concat(userPermissions)
            .ToHashSet(StringComparer.Ordinal);

        // Areas are loaded for administrators too: they see every record, but their
        // voter searches stay inside their areas when they have any (owner, 2026-10-06).
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

        if (isAdministrator)
        {
            return new AccessScope
            {
                UserId = userId,
                IsActive = true,
                SessionsEndedAt = sessionsEndedAt,
                Roles = roles,
                IsAdministrator = true,
                IsSuperAdministrator = roles.Contains(SuperAdministratorRole),
                Permissions = permissions,
                VoterProfileView = profileView,
                StartPage = startPage,
                ConstituencyIds = constituencyIds,
                IslandIds = islandIds,
                VisibleConstituencyIds = visibleConstituencyIds
            };
        }

        return new AccessScope
        {
            UserId = userId,
            IsActive = true,
            SessionsEndedAt = sessionsEndedAt,
            Roles = roles,
            IsAdministrator = false,
            Permissions = permissions,
            VoterProfileView = profileView,
            StartPage = startPage,
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

        return query.InAreas(scope);
    }

    /// <summary>
    /// For voter searches and lists: everyone searches only inside their areas.
    /// An administrator with areas assigned searches only those; one without areas
    /// searches the whole registry (owner decision, 2026-10-06). Opening a record
    /// still uses <see cref="InScope(IQueryable{Voter}, AccessScope)"/>.
    /// </summary>
    public static IQueryable<Voter> InSearchScope(
        this IQueryable<Voter> query,
        AccessScope scope)
    {
        if (scope.IsAdministrator && scope.ConstituencyIds.Count == 0 && scope.IslandIds.Count == 0)
            return query;

        if (!scope.HasAnyScope)
            return query.Where(_ => false);

        return query.InAreas(scope);
    }

    private static IQueryable<Voter> InAreas(
        this IQueryable<Voter> query,
        AccessScope scope)
    {
        var constituencyIds = scope.ConstituencyIds.ToList();
        var islandIds = scope.IslandIds.ToList();

        return query.Where(v =>
            constituencyIds.Contains(v.ConstituencyId) ||
            (v.IslandId.HasValue && islandIds.Contains(v.IslandId.Value)));
    }

    // No InScope for influencers: they are global (owner decision, 2026-10-03).

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
