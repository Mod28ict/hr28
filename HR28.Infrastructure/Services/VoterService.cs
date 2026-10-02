using HR28.Application.DTOs.Common;
using HR28.Application.DTOs.Encounters;
using HR28.Application.DTOs.Influencers;
using HR28.Application.DTOs.Pledges;
using HR28.Application.DTOs.Voters;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HR28.Infrastructure.Services;

public class VoterService : IVoterService
{
    private readonly HR28DbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private async Task<bool> HasFullAccessAsync(Guid userId)
    {
        return await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .AnyAsync(ur =>
                ur.UserId == userId &&
                (
                    ur.Role.Name == "Super Administrator" ||
                    ur.Role.Name == "National Administrator"
                ));
    }


    private readonly IAccessScopeService _accessScopeService;

    public VoterService(
        HR28DbContext dbContext,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IAccessScopeService accessScopeService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _accessScopeService = accessScopeService;
    }

    private async Task<HR28.Application.DTOs.Access.AccessScope> GetCurrentScopeAsync()
    {
        var userId = GetCurrentUserId()
            ?? throw new HR28.Application.Common.AccessDeniedException("You must be signed in.");

        return await _accessScopeService.GetAsync(userId);
    }
    private Guid? GetCurrentUserId()
    {
        var userIdValue = _httpContextAccessor
            .HttpContext?
            .User?
            .FindFirst(ClaimTypes.NameIdentifier)?
            .Value;

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }

    public async Task<VoterDto> CreateVoterAsync(
        CreateVoterDto request)
    {
        var scope = await GetCurrentScopeAsync();

        if (!scope.Allows(request.ConstituencyId, request.IslandId))
        {
            throw new HR28.Application.Common.AccessDeniedException(
                "You can only add voters within your assigned area.");
        }

        if (await _dbContext.Voters.AnyAsync(v => v.NationalId == request.NationalId))
        {
            throw new HR28.Application.Common.BusinessRuleException(
                $"A voter with National ID {request.NationalId} already exists.");
        }

        var voter = new Voter
        {
            Id = Guid.NewGuid(),
            NationalId = request.NationalId,
            FullName = request.FullName,
            Address = request.Address,
            MobileNumber = request.MobileNumber,
            ConstituencyId = request.ConstituencyId,
            IslandId = request.IslandId,
            Remarks = request.Remarks,
            CreatedAt = DateTime.UtcNow,
            SupportStatus = request.SupportStatus 
        };
        _dbContext.Voters.Add(voter);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Create",
            "Voter",
            voter.Id.ToString());

        return new VoterDto
        {
            Id = voter.Id,
            NationalId = voter.NationalId,
            FullName = voter.FullName,
            Address = voter.Address,
            MobileNumber = voter.MobileNumber,
            ConstituencyId = voter.ConstituencyId,
            IslandId = voter.IslandId,
            Remarks = voter.Remarks,
            SupportStatus = voter.SupportStatus
        };
    }

    public async Task<List<VoterDto>>GetVotersAsync(Guid userId)
    {
        var query =
            await GetAuthorizedVoterQueryAsync(
                userId);

        return await query
            .Select(v => new VoterDto
            {
                Id = v.Id,
                NationalId = v.NationalId,
                FullName = v.FullName,
                Address = v.Address,
                MobileNumber = v.MobileNumber,
                ConstituencyId = v.ConstituencyId,
                IslandId = v.IslandId,
                Remarks = v.Remarks,
                SupportStatus = v.SupportStatus
            })
            .ToListAsync();
    }

    public async Task<VoterDto?> GetVoterByIdAsync(
     Guid userId,
     Guid voterId)
    {
        var query =
            await GetAuthorizedVoterQueryAsync(userId);

        return await query
            .Where(v => v.Id == voterId)
            .Select(v => new VoterDto
            {
                Id = v.Id,
                NationalId = v.NationalId,
                FullName = v.FullName,
                Address = v.Address,
                MobileNumber = v.MobileNumber,
                ConstituencyId = v.ConstituencyId,
                IslandId = v.IslandId,

                ConstituencyName =
                    v.Constituency != null
                        ? v.Constituency.Name
                        : string.Empty,

                IslandName =
                    v.Island != null
                        ? v.Island.Name
                        : string.Empty,

                Remarks = v.Remarks,
                SupportStatus = v.SupportStatus
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<VoterDto>> SearchVotersAsync(
        Guid userId,
        string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<VoterDto>();
        }

        var query =
            await GetAuthorizedVoterQueryAsync(userId);

        return await query
            .Where(v =>
                v.FullName.Contains(searchTerm) ||
                v.NationalId.Contains(searchTerm))
            .OrderBy(v => v.FullName)
            .Take(50)
            .Select(v => new VoterDto
            {
                Id = v.Id,
                NationalId = v.NationalId,
                FullName = v.FullName,
                Address = v.Address,
                MobileNumber = v.MobileNumber,
                ConstituencyId = v.ConstituencyId,
                IslandId = v.IslandId,
                Remarks = v.Remarks,
                SupportStatus = v.SupportStatus
            })
            .ToListAsync();
    }
    public async Task UpdateVoterAsync(
    Guid id,
    UpdateVoterDto request)
    {
        var scope = await GetCurrentScopeAsync();

        // Only voters inside the scope can be found; others behave as not found.
        var voter = await _dbContext.Voters
            .InScope(scope)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (voter == null)
            throw new KeyNotFoundException("Voter not found.");

        // Constituency and island are protected fields: only administrators may change them.
        var movingArea =
            voter.ConstituencyId != request.ConstituencyId ||
            voter.IslandId != request.IslandId;

        if (movingArea && !scope.IsAdministrator)
        {
            throw new HR28.Application.Common.AccessDeniedException(
                "Only an Administrator can move a voter to another constituency or island.");
        }

        if (!scope.Allows(request.ConstituencyId, request.IslandId))
        {
            throw new HR28.Application.Common.AccessDeniedException(
                "You can only move voters within your assigned area.");
        }

        if (voter.NationalId != request.NationalId &&
            await _dbContext.Voters.AnyAsync(v => v.Id != id && v.NationalId == request.NationalId))
        {
            throw new HR28.Application.Common.BusinessRuleException(
                $"Another voter already has National ID {request.NationalId}.");
        }

        voter.NationalId = request.NationalId;
        voter.FullName = request.FullName;
        voter.Address = request.Address;
        voter.MobileNumber = request.MobileNumber;
        voter.ConstituencyId = request.ConstituencyId;
        voter.IslandId = request.IslandId;
        voter.SupportStatus = request.SupportStatus;
        voter.Remarks = request.Remarks ?? string.Empty;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Update",
            "Voter",
            voter.Id.ToString());
    }
    public async Task DeleteVoterAsync(Guid id)
    {
        var voter = await _dbContext.Voters
            .FirstOrDefaultAsync(v => v.Id == id);

        if (voter == null)
            throw new KeyNotFoundException("Voter not found.");

        _dbContext.Voters.Remove(voter);

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Delete",
            "Voter",
            id.ToString());
    }
    public async Task<VoterProfileDto> GetProfileAsync(
        Guid userId,
        Guid voterId)
    {
        var voter =
            await GetVoterByIdAsync(
                userId,
                voterId);

        if (voter == null)
        {
            throw new KeyNotFoundException(
                "Voter not found.");
        }

        var influencers =
            await _dbContext.VoterInfluencers
                .Where(x =>
                    x.VoterId == voterId)
                .Join(
                    _dbContext.Influencers,
                    vi => vi.InfluencerId,
                    i => i.Id,
                    (vi, i) =>
                        new VoterInfluencerDto
                        {
                            InfluencerId = i.Id,
                            FullName = i.FullName,
                            NationalId = i.NationalId,
                            ContactNumber =
                                i.ContactNumber,
                            RelationshipType =
                                vi.RelationshipType
                        })
                .ToListAsync();

        var encounters =
            await _dbContext.Encounters
                .Where(x =>
                    x.VoterId == voterId)
                .OrderByDescending(x =>
                    x.EncounterDate)
                .Select(x =>
                    new EncounterDto
                    {
                        Id = x.Id,
                        VoterId = x.VoterId,
                        EncounterDate =
                            x.EncounterDate,
                        EncounterType =
                            x.EncounterType,
                        Outcome = x.Outcome,
                        Notes = x.Notes,
                        RecordedByUserId =
                            x.RecordedByUserId
                    })
                .ToListAsync();

        var pledges =
            await _dbContext.Pledges
                .Where(x =>
                    x.VoterId == voterId)
                .OrderByDescending(x =>
                    x.PledgeDate)
                .Select(x =>
                    new PledgeDto
                    {
                        Id = x.Id,
                        VoterId = x.VoterId,
                        CreatedByUserId =
                            x.CreatedByUserId,
                        AssignedToUserId =
                            x.AssignedToUserId,
                        PledgeDate = x.PledgeDate,
                        Title = x.Title,
                        Description = x.Description,
                        Status = x.Status,
                        DueDate = x.DueDate,
                        FulfilledDate =
                            x.FulfilledDate,
                        ResolutionNotes =
                            x.ResolutionNotes
                    })
                .ToListAsync();

        return new VoterProfileDto
        {
            Voter = voter,
            Influencers = influencers,
            Encounters = encounters,
            Pledges = pledges
        };
    }

    // Keep your existing influencer,
    // encounter and pledge queries below.
    public async Task<NationalIdCheckDto> CheckNationalIdAsync(
        Guid userId,
        string nationalId,
        Guid? excludeVoterId)
    {
        var id = (nationalId ?? string.Empty).Trim().ToUpperInvariant();

        if (id.Length == 0)
            return new NationalIdCheckDto();

        var existing = await _dbContext.Voters
            .AsNoTracking()
            .Where(v => v.NationalId == id && v.Id != excludeVoterId)
            .Select(v => new { v.Id })
            .FirstOrDefaultAsync();

        if (existing == null)
            return new NationalIdCheckDto();

        // Only reveal who it is if the caller is allowed to see that voter.
        var visible = await (await GetAuthorizedVoterQueryAsync(userId))
            .Where(v => v.Id == existing.Id)
            .Select(v => new { v.Id, v.FullName })
            .FirstOrDefaultAsync();

        return new NationalIdCheckDto
        {
            Exists = true,
            VoterId = visible?.Id,
            FullName = visible?.FullName
        };
    }

    public async Task<List<VoterDto>> GetRecentAsync(
        Guid userId,
        int count = 10)
    {
        var query = await GetAuthorizedVoterQueryAsync(userId);

        return await query
            .AsNoTracking()
            .OrderByDescending(v => v.CreatedAt)
            .Take(count)
            .Select(v => new VoterDto
            {
                Id = v.Id,
                NationalId = v.NationalId,
                FullName = v.FullName,
                Address = v.Address,
                MobileNumber = v.MobileNumber,
                ConstituencyId = v.ConstituencyId,
                IslandId = v.IslandId,
                ConstituencyName = v.Constituency != null ? v.Constituency.Name : string.Empty,
                IslandName = v.Island != null ? v.Island.Name : string.Empty,
                Remarks = v.Remarks,
                SupportStatus = v.SupportStatus
            })
            .ToListAsync();
    }
    private async Task<IQueryable<Voter>>
        GetAuthorizedVoterQueryAsync(Guid userId)
    {
        var query = _dbContext.Voters.AsQueryable();

        if (await HasFullAccessAsync(userId))
        {
            return query;
        }

        var scopes = await _dbContext.UserScopes
            .Where(x => x.UserId == userId)
            .ToListAsync();

        if (scopes.Count == 0)
        {
            return query.Where(_ => false);
        }

        var constituencyOnlyIds = scopes
            .Where(x =>
                x.ConstituencyId.HasValue &&
                !x.IslandId.HasValue)
            .Select(x => x.ConstituencyId!.Value)
            .Distinct()
            .ToList();

        var islandIds = scopes
            .Where(x => x.IslandId.HasValue)
            .Select(x => x.IslandId!.Value)
            .Distinct()
            .ToList();

        return query.Where(v =>
            constituencyOnlyIds.Contains(
                v.ConstituencyId)
            ||
            (
                v.IslandId.HasValue &&
                islandIds.Contains(
                    v.IslandId.Value)
            ));
    }

    public async Task<PagedResult<VoterDto>>
        GetVotersAsync(
            Guid userId,
            int page,
            int pageSize,
            VoterListFilter filter)
    {
        page = page < 1
            ? 1
            : page;

        pageSize = pageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => pageSize
        };

        var query =
            await GetAuthorizedVoterQueryAsync(
                userId);

        query = query.AsNoTracking();

        // Filters narrow the user's areas; they can never widen them.
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = Limit(filter.SearchTerm);

            query = query.Where(v =>
                v.NationalId.Contains(term) ||
                v.FullName.Contains(term) ||
                v.MobileNumber.Contains(term) ||
                (v.Island != null && v.Island.Name.Contains(term)) ||
                v.RegisteredIsland.Contains(term));
        }

        if (filter.ConstituencyId.HasValue)
            query = query.Where(v => v.ConstituencyId == filter.ConstituencyId.Value);

        if (filter.IslandId.HasValue)
            query = query.Where(v => v.IslandId == filter.IslandId.Value);

        if (!string.IsNullOrWhiteSpace(filter.House))
        {
            var house = Limit(filter.House);
            query = query.Where(v => v.Address.Contains(house));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            SupportStatuses.Contains(filter.Status.Trim()))
        {
            var status = filter.Status.Trim();
            query = query.Where(v => v.SupportStatus == status);
        }

        var totalCount =
            await query.CountAsync();

        var items =
            await query
                .OrderBy(v => v.FullName)
                .ThenBy(v => v.NationalId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new VoterDto
                {
                    Id = v.Id,
                    NationalId = v.NationalId,
                    FullName = v.FullName,
                    Address = v.Address,
                    MobileNumber = v.MobileNumber,
                    ConstituencyId =
                        v.ConstituencyId,
                    IslandId = v.IslandId,
                    ConstituencyName = v.Constituency != null ? v.Constituency.Name : string.Empty,
                    IslandName = v.Island != null ? v.Island.Name : string.Empty,
                    Remarks = v.Remarks,
                    SupportStatus =
                        v.SupportStatus
                })
                .ToListAsync();

        return new PagedResult<VoterDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public static readonly string[] SupportStatuses =
        { "Supporter", "Undecided", "Neutral", "Opponent" };

    /// <summary>Trims search text and caps its length so a huge value can't slow the query.</summary>
    private static string Limit(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 100 ? trimmed[..100] : trimmed;
    }
}
