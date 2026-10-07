using HR28.Application.Common;
using HR28.Application.DTOs.Users;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Domain.Enums;
using HR28.Infrastructure.Data;
using HR28.Infrastructure.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly HR28DbContext _dbContext;
    private readonly IAuthorizationCodeHasher _codeHasher;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccessScopeService _accessScopeService;
    private readonly ISmsSender _smsSender;

    public UserService(
        HR28DbContext dbContext,
        IAuthorizationCodeHasher codeHasher,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IAccessScopeService accessScopeService,
        ISmsSender smsSender)
    {
        _dbContext = dbContext;
        _codeHasher = codeHasher;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _accessScopeService = accessScopeService;
        _smsSender = smsSender;
    }

    private Guid? GetCurrentUserId()
    {
        var value = _httpContextAccessor.HttpContext?.User
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>Generates a code whose hash is not already in use.</summary>
    private async Task<(string Code, string Hash)> NewUniqueCodeAsync()
    {
        while (true)
        {
            var code = AuthorizationCodeGenerator.Generate();
            var hash = _codeHasher.Hash(code);

            if (!await _dbContext.Users.AnyAsync(u => u.AuthorizationCodeHash == hash))
                return (code, hash);
        }
    }

    public async Task<UserDto> CreateUserAsync(
        CreateUserDto request)
    {
        request.NationalId = MaldivesFormats.CleanNationalId(request.NationalId);
        request.MobileNumber = MaldivesFormats.CleanMobile(request.MobileNumber);

        MaldivesFormats.RequireNationalId(request.NationalId);

        // Required: the sign-in code is sent to this number by SMS.
        MaldivesFormats.RequireMobile(request.MobileNumber, "Mobile number", required: true);

        if (await _dbContext.Users.AnyAsync(u => u.NationalId == request.NationalId))
            throw new BusinessRuleException($"There is already a user with National ID {request.NationalId}.");

        var (authorizationCode, codeHash) = await NewUniqueCodeAsync();

        var user = new User
        {
            Id = Guid.NewGuid(),
            NationalId = request.NationalId,
            FullName = (request.FullName ?? string.Empty).Trim(),
            Address = request.Address?.Trim() ?? string.Empty,
            MobileNumber = request.MobileNumber,
            Email = request.Email?.Trim() ?? string.Empty,
            Designation = request.Designation?.Trim() ?? string.Empty,
            Remarks = request.Remarks?.Trim() ?? string.Empty,
            AuthorizationCodeHash = codeHash,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId() ?? Guid.Empty
        };

        _dbContext.Users.Add(user);

        _dbContext.AuthorizationCodeHistories.Add(new AuthorizationCodeHistory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AuthorizationCodeHash = codeHash,
            CreatedAt = DateTime.UtcNow,
            Reason = "Issued when the account was created"
        });

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(), "Create", "User", user.Id.ToString());

        return new UserDto
        {
            Id = user.Id,
            NationalId = user.NationalId,
            FullName = user.FullName,
            Address = user.Address,
            MobileNumber = user.MobileNumber,
            Email = user.Email,
            Designation = user.Designation,

            // Returned this once so the administrator can hand it over.
            AuthorizationCode = authorizationCode,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<string?> ResetAuthorizationCodeAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return null;

        var (code, hash) = await NewUniqueCodeAsync();

        var now = DateTime.UtcNow;

        var previous = await _dbContext.AuthorizationCodeHistories
            .Where(h => h.UserId == userId && h.RevokedAt == null)
            .ToListAsync();

        foreach (var entry in previous)
            entry.RevokedAt = now;

        user.AuthorizationCodeHash = hash;
        user.AuthorizationCode = null;

        _dbContext.AuthorizationCodeHistories.Add(new AuthorizationCodeHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AuthorizationCodeHash = hash,
            CreatedAt = now,
            Reason = "Reset by an administrator"
        });

        // Any code already sent by SMS for the old authorization code stops working too.
        var openOtps = await _dbContext.OtpRequests
            .Where(o => o.UserId == userId && !o.IsUsed)
            .ToListAsync();

        foreach (var otp in openOtps)
            otp.IsUsed = true;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(), "Reset authorization code", "User", userId.ToString());

        return code;
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        return await QueryUsersAsync(_dbContext.Users);
    }

    public async Task<UserPageDto> SearchUsersAsync(int page, int pageSize, string? search, string? status)
    {
        page = Math.Max(1, page);
        pageSize = pageSize is 10 or 20 or 50 or 100 ? pageSize : 20;

        var query = _dbContext.Users.AsNoTracking();

        // Name, ID card, phone, email, designation, role or area.
        var term = (search ?? string.Empty).Trim();
        if (term.Length > 100)
            term = term[..100];

        if (term.Length > 0)
        {
            query = query.Where(u =>
                u.FullName.Contains(term) ||
                u.NationalId.Contains(term) ||
                u.MobileNumber.Contains(term) ||
                u.Email.Contains(term) ||
                u.Designation.Contains(term) ||
                u.UserRoles.Any(ur => ur.Role.Name.Contains(term)) ||
                u.UserScopes.Any(s =>
                    (s.Constituency != null && s.Constituency.Name.Contains(term)) ||
                    (s.Island != null && s.Island.Name.Contains(term))));
        }

        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.IsActive);
        else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => !u.IsActive);

        var totalCount = await query.CountAsync();

        var ids = await query
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => u.Id)
            .ToListAsync();

        var items = (await QueryUsersAsync(_dbContext.Users.Where(u => ids.Contains(u.Id))))
            .OrderBy(u => ids.IndexOf(u.Id))
            .ToList();

        // The cards above the list count every user, not just this page.
        var totals = await _dbContext.Users
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(u => u.IsActive),
                NoRole = g.Count(u => !u.UserRoles.Any())
            })
            .FirstOrDefaultAsync();

        return new UserPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalUsers = totals?.Total ?? 0,
            ActiveUsers = totals?.Active ?? 0,
            InactiveUsers = (totals?.Total ?? 0) - (totals?.Active ?? 0),
            NoRoleUsers = totals?.NoRole ?? 0
        };
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        return (await QueryUsersAsync(_dbContext.Users.Where(u => u.Id == id)))
            .FirstOrDefault();
    }

    /// <summary>Loads users with all their roles and areas (no codes).</summary>
    private static async Task<List<UserDto>> QueryUsersAsync(IQueryable<User> users)
    {
        var rows = await users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(user => new
            {
                user.Id,
                user.NationalId,
                user.FullName,
                user.Address,
                user.MobileNumber,
                user.Email,
                user.Designation,
                user.IsActive,
                user.Remarks,
                user.LastLoginAt,
                Roles = user.UserRoles.Select(x => x.Role.Name).ToList(),
                Scopes = user.UserScopes
                    .Select(s => new UserScopeDto
                    {
                        Id = s.Id,
                        ConstituencyId = s.ConstituencyId,
                        ConstituencyName = s.Constituency != null ? s.Constituency.Name : string.Empty,
                        IslandId = s.IslandId,
                        IslandName = s.Island != null ? s.Island.Name : null
                    })
                    .ToList()
            })
            .ToListAsync();

        return rows.Select(r =>
        {
            var roles = RoleOrder.Sort(r.Roles);
            var scopes = r.Scopes
                .OrderBy(s => s.ConstituencyName)
                .ThenBy(s => s.IslandName)
                .ToList();

            return new UserDto
            {
                Id = r.Id,
                NationalId = r.NationalId,
                FullName = r.FullName,
                Address = r.Address,
                MobileNumber = r.MobileNumber,
                Email = r.Email,
                Designation = r.Designation,
                IsActive = r.IsActive,
                Remarks = r.Remarks ?? string.Empty,
                LastLoginAt = r.LastLoginAt,
                Roles = roles,
                RoleName = roles.FirstOrDefault(),
                Scopes = scopes,
                ConstituencyName = scopes.FirstOrDefault()?.ConstituencyName,
                IslandName = scopes.FirstOrDefault()?.IslandName
            };
        }).ToList();
    }
    /// <summary>True when the user is the only active Administrator left.</summary>
    private async Task<bool> IsLastActiveAdministratorAsync(Guid userId) =>
        await _dbContext.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.Role.Name == AccessScopeService.SuperAdministratorRole) &&
        !await _dbContext.UserRoles.AnyAsync(ur =>
            ur.UserId != userId &&
            ur.Role.Name == AccessScopeService.SuperAdministratorRole &&
            ur.User.IsActive);

    public async Task UpdateUserAsync(Guid userId, UpdateUserDto request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        var nationalId = MaldivesFormats.CleanNationalId(request.NationalId);
        var mobile = MaldivesFormats.CleanMobile(request.MobileNumber);
        var fullName = (request.FullName ?? string.Empty).Trim();

        MaldivesFormats.RequireNationalId(nationalId);
        MaldivesFormats.RequireMobile(mobile, "Mobile number", required: true);

        if (fullName.Length == 0)
            throw new BusinessRuleException("Please enter the full name.");

        if (nationalId != user.NationalId &&
            await _dbContext.Users.AnyAsync(u => u.Id != userId && u.NationalId == nationalId))
            throw new BusinessRuleException($"There is already a user with National ID {nationalId}.");

        if (!request.IsActive && user.IsActive)
            await RequireCanDeactivateAsync(userId);

        var email = (request.Email ?? string.Empty).Trim();
        var designation = (request.Designation ?? string.Empty).Trim();
        var address = (request.Address ?? string.Empty).Trim();
        var remarks = (request.Remarks ?? string.Empty).Trim();

        // Identity values are recorded; free text only as "changed".
        var changes = new List<string>();
        if (user.FullName != fullName) changes.Add($"name \"{user.FullName}\" → \"{fullName}\"");
        if (user.NationalId != nationalId) changes.Add($"National ID {user.NationalId} → {nationalId}");
        var oldMobile = user.MobileNumber;
        if (oldMobile != mobile) changes.Add($"mobile {oldMobile} → {mobile}");
        if ((user.Email ?? string.Empty) != email) changes.Add("email");
        if ((user.Designation ?? string.Empty) != designation) changes.Add("designation");
        if ((user.Address ?? string.Empty) != address) changes.Add("address");
        if ((user.Remarks ?? string.Empty) != remarks) changes.Add("remarks");
        if (user.IsActive != request.IsActive) changes.Add(request.IsActive ? "account activated" : "account deactivated");

        if (changes.Count == 0)
            return;

        user.FullName = fullName;
        user.NationalId = nationalId;
        user.MobileNumber = mobile;
        user.Email = email;
        user.Designation = designation;
        user.Address = address;
        user.Remarks = remarks;
        user.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();

        // Deactivation applies on the person's next request.
        _accessScopeService.Invalidate(userId);

        await _auditService.LogAsync(GetCurrentUserId(), "Update: " + string.Join(", ", changes), "User", userId.ToString());

        // Whoever controls the number controls the account: warn the old number.
        if (oldMobile != mobile && !string.IsNullOrWhiteSpace(oldMobile))
        {
            await _smsSender.SendAsync(oldMobile,
                "HR28: the mobile number on your account was changed by an administrator. " +
                "If you did not ask for this, contact your HR28 administrator.");
        }
    }

    private async Task RequireCanDeactivateAsync(Guid userId)
    {
        if (userId == GetCurrentUserId())
            throw new BusinessRuleException("You can't deactivate your own account.");

        if (await IsLastActiveAdministratorAsync(userId))
            throw new BusinessRuleException("This is the only active Administrator, so the account must stay active.");
    }

    public async Task SetActiveAsync(Guid userId, bool isActive)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.IsActive == isActive)
            return;

        if (!isActive)
            await RequireCanDeactivateAsync(userId);

        user.IsActive = isActive;

        // A locked-out account starts fresh when it is turned back on.
        if (isActive)
        {
            user.FailedLoginAttempts = 0;
            user.IsLocked = false;
            user.LockedUntilUtc = null;
        }

        await _dbContext.SaveChangesAsync();

        // Deactivation applies on the person's next request.
        _accessScopeService.Invalidate(userId);

        await _auditService.LogAsync(
            GetCurrentUserId(),
            isActive ? "Account activated" : "Account deactivated",
            "User",
            userId.ToString());
    }

    public async Task<string> DeleteUserAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (userId == GetCurrentUserId())
            throw new BusinessRuleException("You can't delete your own account.");

        if (await IsLastActiveAdministratorAsync(userId))
            throw new BusinessRuleException("This is the only active Administrator, so the account can't be deleted.");

        var encounters = await _dbContext.Encounters.CountAsync(e => e.RecordedByUserId == userId);
        var pledges = await _dbContext.Pledges.CountAsync(p => p.CreatedByUserId == userId || p.AssignedToUserId == userId);

        if (encounters + pledges > 0)
        {
            throw new BusinessRuleException(
                $"{user.FullName} has recorded {encounters:N0} encounter{(encounters == 1 ? "" : "s")} and " +
                $"{pledges:N0} pledge{(pledges == 1 ? "" : "s")}, and those records must keep who made them, " +
                "so the account can't be deleted. Deactivate it instead (Edit → Account is active).");
        }

        var label = $"{user.FullName} ({user.NationalId})";

        // Areas don't cascade; roles, rights, codes and sign-in requests do.
        _dbContext.UserScopes.RemoveRange(_dbContext.UserScopes.Where(s => s.UserId == userId));
        _dbContext.Users.Remove(user);

        await _dbContext.SaveChangesAsync();

        _accessScopeService.Invalidate(userId);

        // The account is gone, so the audit entry carries the name.
        await _auditService.LogAsync(GetCurrentUserId(), $"Delete (permanent): {label}", "User", userId.ToString());

        return user.FullName;
    }

    /// <summary>
    /// Sets the user's role, replacing any previous role (the screen offers a single
    /// choice). Previously this only added roles, so a demoted user kept old rights.
    /// </summary>
    public Task AssignRoleAsync(Guid userId, Guid roleId) =>
        SetRolesAsync(userId, new[] { roleId });

    /// <summary>
    /// Replaces the user's roles with exactly this set. Permissions are the
    /// combination of all roles. At least one role is required, and the last
    /// active Administrator cannot lose that role.
    /// </summary>
    public async Task SetRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds)
    {
        var wanted = roleIds.Distinct().ToList();

        if (wanted.Count == 0)
            throw new BusinessRuleException("Choose at least one role.");

        if (!await _dbContext.Users.AnyAsync(u => u.Id == userId))
            throw new KeyNotFoundException("User not found.");

        var roles = await _dbContext.Roles
            .Where(r => wanted.Contains(r.Id))
            .ToListAsync();

        if (roles.Count != wanted.Count)
            throw new BusinessRuleException("One of the chosen roles is not valid. Please reload the page.");

        var current = await _dbContext.UserRoles
            .Include(x => x.Role)
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var removed = current.Where(c => !wanted.Contains(c.RoleId)).ToList();
        var added = roles.Where(r => current.All(c => c.RoleId != r.Id)).ToList();

        if (removed.Count == 0 && added.Count == 0)
            return;

        // Never remove the last active Administrator, or nobody could manage users.
        if (removed.Any(x => x.Role.Name == AccessScopeService.SuperAdministratorRole))
        {
            var otherSuperAdmins = await _dbContext.UserRoles.CountAsync(x =>
                x.UserId != userId &&
                x.User.IsActive &&
                x.Role.Name == AccessScopeService.SuperAdministratorRole);

            if (otherSuperAdmins == 0)
            {
                throw new BusinessRuleException(
                    "This is the only Administrator. Make another user an Administrator first.");
            }
        }

        _dbContext.UserRoles.RemoveRange(removed);

        foreach (var role in added)
            _dbContext.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });

        await _dbContext.SaveChangesAsync();

        _accessScopeService.Invalidate(userId);

        var changes = new List<string>();

        if (added.Count > 0)
            changes.Add("added " + string.Join(", ", RoleOrder.Sort(added.Select(r => r.Name))));

        if (removed.Count > 0)
            changes.Add("removed " + string.Join(", ", RoleOrder.Sort(removed.Select(r => r.Role.Name))));

        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Roles changed: " + string.Join("; ", changes),
            "User",
            userId.ToString());
    }

    /// <summary>Kept for the old single-scope endpoint: now adds an area.</summary>
    public async Task AssignScopeAsync(
        Guid userId,
        Guid? constituencyId,
        Guid? islandId)
    {
        await AddScopeAsync(userId, constituencyId, islandId);
    }

    /// <summary>
    /// Adds one area (a whole constituency, or one island in it). A user can
    /// have many; they see records in any of them.
    /// </summary>
    public async Task<UserScopeDto> AddScopeAsync(Guid userId, Guid? constituencyId, Guid? islandId)
    {
        if (constituencyId == null)
            throw new BusinessRuleException("Please choose a constituency.");

        if (!await _dbContext.Users.AnyAsync(u => u.Id == userId))
            throw new KeyNotFoundException("User not found.");

        var constituencyName = await _dbContext.Constituencies
            .Where(c => c.Id == constituencyId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync()
            ?? throw new BusinessRuleException("Please choose a valid constituency.");

        string? islandName = null;

        if (islandId != null)
        {
            var belongs =
                await _dbContext.ConstituencyIslands.AnyAsync(ci =>
                    ci.ConstituencyId == constituencyId && ci.IslandId == islandId) ||
                await _dbContext.Islands.AnyAsync(i =>
                    i.Id == islandId && i.ConstituencyId == constituencyId);

            if (!belongs)
                throw new BusinessRuleException("That island is not part of the chosen constituency.");

            islandName = await _dbContext.Islands
                .Where(i => i.Id == islandId)
                .Select(i => i.Name)
                .FirstAsync();
        }

        var existing = await _dbContext.UserScopes
            .Where(s => s.UserId == userId && s.ConstituencyId == constituencyId)
            .ToListAsync();

        if (existing.Any(s => s.IslandId == null))
        {
            throw new BusinessRuleException(islandId == null
                ? $"{constituencyName} is already assigned."
                : $"{constituencyName} is already assigned with all its islands, which includes {islandName}.");
        }

        if (islandId != null && existing.Any(s => s.IslandId == islandId))
            throw new BusinessRuleException($"{islandName} is already assigned.");

        // Adding the whole constituency makes single-island areas in it unnecessary.
        var replaced = islandId == null ? existing : new List<UserScope>();
        _dbContext.UserScopes.RemoveRange(replaced);

        var scope = new UserScope
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConstituencyId = constituencyId,
            IslandId = islandId
        };

        _dbContext.UserScopes.Add(scope);

        await _dbContext.SaveChangesAsync();

        _accessScopeService.Invalidate(userId);

        var dto = new UserScopeDto
        {
            Id = scope.Id,
            ConstituencyId = constituencyId,
            ConstituencyName = constituencyName,
            IslandId = islandId,
            IslandName = islandName
        };

        await _auditService.LogAsync(
            GetCurrentUserId(),
            $"Area added: {dto.Label}" +
                (replaced.Count > 0 ? $" (replaces {replaced.Count} single-island area{(replaced.Count == 1 ? "" : "s")} in it)" : ""),
            "User",
            userId.ToString());

        return dto;
    }

    public async Task RemoveScopeAsync(Guid userId, Guid scopeId)
    {
        var scope = await _dbContext.UserScopes
            .Include(s => s.Constituency)
            .Include(s => s.Island)
            .FirstOrDefaultAsync(s => s.Id == scopeId && s.UserId == userId)
            ?? throw new KeyNotFoundException("Area not found.");

        var label = new UserScopeDto
        {
            ConstituencyName = scope.Constituency?.Name ?? string.Empty,
            IslandName = scope.Island?.Name
        }.Label;

        _dbContext.UserScopes.Remove(scope);

        await _dbContext.SaveChangesAsync();

        _accessScopeService.Invalidate(userId);

        await _auditService.LogAsync(
            GetCurrentUserId(),
            $"Area removed: {label}",
            "User",
            userId.ToString());
    }

}