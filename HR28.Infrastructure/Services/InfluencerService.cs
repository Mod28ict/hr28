using HR28.Application.Common;
using HR28.Application.DTOs.Access;
using HR28.Application.DTOs.Influencers;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class InfluencerService : IInfluencerService
{
    private readonly HR28DbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccessScopeService _accessScopeService;

    public InfluencerService(
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

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor
            .HttpContext?
            .User
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
            return null;

        return Guid.Parse(userIdClaim.Value);
    }

    private async Task<AccessScope> GetCurrentScopeAsync()
    {
        var userId = GetCurrentUserId()
            ?? throw new AccessDeniedException("You must be signed in.");

        return await _accessScopeService.GetAsync(userId);
    }

    private async Task EnsureVoterInScopeAsync(AccessScope scope, Guid voterId)
    {
        var visible = await _dbContext.Voters
            .InScope(scope)
            .AnyAsync(v => v.Id == voterId);

        // Out-of-scope records are reported as not found so their existence isn't revealed.
        if (!visible)
            throw new KeyNotFoundException();
    }

    // Influencers are global (owner decision, 2026-10-03): everyone sees all of them.
    // Voters stay limited to the user's areas.
    private async Task EnsureInfluencerExistsAsync(Guid influencerId)
    {
        if (!await _dbContext.Influencers.AnyAsync(i => i.Id == influencerId))
            throw new KeyNotFoundException();
    }

    /// <summary>Checks shared by create and edit. Returns the cleaned National ID and name.</summary>
    private async Task<(string NationalId, string FullName)> ValidateAsync(
        CreateInfluencerDto request,
        Guid? existingId)
    {
        var nationalId = (request.NationalId ?? string.Empty).Trim().ToUpperInvariant();
        var fullName = (request.FullName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(nationalId))
            throw new BusinessRuleException("National ID is required.");

        if (string.IsNullOrWhiteSpace(fullName))
            throw new BusinessRuleException("Full name is required.");

        var constituencyExists = await _dbContext.Constituencies
            .AnyAsync(c => c.Id == request.ConstituencyId);

        if (!constituencyExists)
            throw new BusinessRuleException("Please choose a valid constituency.");

        if (request.IslandId.HasValue)
        {
            var islandBelongs =
                await _dbContext.ConstituencyIslands.AnyAsync(ci =>
                    ci.ConstituencyId == request.ConstituencyId &&
                    ci.IslandId == request.IslandId.Value) ||
                await _dbContext.Islands.AnyAsync(i =>
                    i.Id == request.IslandId.Value &&
                    i.ConstituencyId == request.ConstituencyId);

            if (!islandBelongs)
                throw new BusinessRuleException("The selected island does not belong to the selected constituency.");
        }


        var duplicate = await _dbContext.Influencers
            .AnyAsync(i => i.NationalId == nationalId && i.Id != existingId);

        if (duplicate)
            throw new BusinessRuleException($"An influencer with National ID {nationalId} already exists.");

        return (nationalId, fullName);
    }

    public async Task<InfluencerDto> CreateAsync(
        CreateInfluencerDto request)
    {
        var (nationalId, fullName) = await ValidateAsync(request, null);

        var influencer = new Influencer
        {
            Id = Guid.NewGuid(),
            NationalId = nationalId,
            FullName = fullName,
            Address = request.Address?.Trim() ?? string.Empty,
            ContactNumber = request.ContactNumber?.Trim() ?? string.Empty,
            ConstituencyId = request.ConstituencyId,
            IslandId = request.IslandId,
            Remarks = request.Remarks?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Influencers.Add(influencer);

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Create",
            "Influencer",
            influencer.Id.ToString());

        return await _dbContext.Influencers
            .Where(i => i.Id == influencer.Id)
            .Select(ToDto())
            .FirstAsync();
    }

    public async Task<InfluencerDto> GetByIdAsync(Guid id)
    {
        return await _dbContext.Influencers
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(ToDto())
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Influencer not found.");
    }

    public async Task<InfluencerDto> UpdateAsync(Guid id, CreateInfluencerDto request)
    {
        var scope = await GetCurrentScopeAsync();

        if (!scope.HasPermission(PermissionCatalog.InfluencersEdit))
            throw new AccessDeniedException("You don't have permission to edit influencers. Ask your Administrator.");

        var influencer = await _dbContext.Influencers
            .FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new KeyNotFoundException("Influencer not found.");

        var (nationalId, fullName) = await ValidateAsync(request, id);

        var address = request.Address?.Trim() ?? string.Empty;
        var contact = request.ContactNumber?.Trim() ?? string.Empty;
        var remarks = request.Remarks?.Trim() ?? string.Empty;

        // Record which fields changed (values for identity fields, not free text).
        var changes = new List<string>();

        if (influencer.NationalId != nationalId) changes.Add($"National ID \"{influencer.NationalId}\" → \"{nationalId}\"");
        if (influencer.FullName != fullName) changes.Add($"name \"{influencer.FullName}\" → \"{fullName}\"");
        if (influencer.ContactNumber != contact) changes.Add("contact number");
        if (influencer.Address != address) changes.Add("address");
        if (influencer.ConstituencyId != request.ConstituencyId || influencer.IslandId != request.IslandId) changes.Add("area");
        if (influencer.Remarks != remarks) changes.Add("remarks");

        if (changes.Count == 0)
            return await GetByIdAsync(id);

        influencer.NationalId = nationalId;
        influencer.FullName = fullName;
        influencer.ContactNumber = contact;
        influencer.Address = address;
        influencer.ConstituencyId = request.ConstituencyId;
        influencer.IslandId = request.IslandId;
        influencer.Remarks = remarks;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Update: " + string.Join(", ", changes),
            "Influencer",
            id.ToString());

        return await GetByIdAsync(id);
    }

    public async Task<string> DeleteAsync(Guid id)
    {
        var scope = await GetCurrentScopeAsync();

        if (!scope.HasPermission(PermissionCatalog.InfluencersDelete))
            throw new AccessDeniedException("You don't have permission to delete influencers. Ask your Administrator.");

        var influencer = await _dbContext.Influencers
            .FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new KeyNotFoundException("Influencer not found.");

        var links = await _dbContext.VoterInfluencers
            .Where(vi => vi.InfluencerId == id)
            .ToListAsync();

        var name = influencer.FullName;
        var nationalId = influencer.NationalId;

        _dbContext.VoterInfluencers.RemoveRange(links);
        _dbContext.Influencers.Remove(influencer);

        await _dbContext.SaveChangesAsync();

        // The record is gone, so the audit entry carries the name itself.
        await _auditService.LogAsync(
            GetCurrentUserId(),
            $"Delete (permanent): {name} ({nationalId}), {links.Count} voter link{(links.Count == 1 ? "" : "s")} removed",
            "Influencer",
            id.ToString());

        return name;
    }

    public async Task<List<InfluencerDto>> GetAllAsync()
    {
        return await _dbContext.Influencers
            .AsNoTracking()
            .OrderBy(i => i.FullName)
            .Select(ToDto())
            .ToListAsync();
    }

    public async Task LinkToVoterAsync(
        LinkInfluencerDto request)
    {
        var scope = await GetCurrentScopeAsync();

        await EnsureVoterInScopeAsync(scope, request.VoterId);
        await EnsureInfluencerExistsAsync(request.InfluencerId);

        // An influencer can be linked to a voter only once. Changing the
        // relationship is done with Edit (UpdateRelationshipAsync), not by linking again.
        var existingLink =
            await _dbContext.VoterInfluencers
                .AsNoTracking()
                .Where(x =>
                    x.VoterId == request.VoterId &&
                    x.InfluencerId == request.InfluencerId)
                .Select(x => new { x.RelationshipType, x.Influencer.FullName })
                .FirstOrDefaultAsync();

        if (existingLink != null)
        {
            var already = string.IsNullOrWhiteSpace(existingLink.RelationshipType)
                ? ""
                : $" (as {existingLink.RelationshipType})";

            throw new BusinessRuleException(
                $"{existingLink.FullName} is already linked to this voter{already}. " +
                "To change the relationship, use Edit on the voter's profile.");
        }

        _dbContext.VoterInfluencers.Add(new VoterInfluencer
        {
            Id = Guid.NewGuid(),
            VoterId = request.VoterId,
            InfluencerId = request.InfluencerId,
            RelationshipType = request.RelationshipType,
            LinkedAt = DateTime.UtcNow
        });

        const string auditAction = "Link To Voter";

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(),
            auditAction,
            "Influencer",
            request.InfluencerId.ToString());
    }

    public async Task<List<VoterInfluencerDto>>
        GetByVoterIdAsync(Guid voterId)
    {
        var scope = await GetCurrentScopeAsync();

        await EnsureVoterInScopeAsync(scope, voterId);

        return await _dbContext.VoterInfluencers
            .AsNoTracking()
            .Where(x => x.VoterId == voterId)
            .Select(x => new VoterInfluencerDto
            {
                InfluencerId = x.Influencer.Id,
                FullName = x.Influencer.FullName,
                NationalId = x.Influencer.NationalId,
                ContactNumber = x.Influencer.ContactNumber,
                RelationshipType = x.RelationshipType
            })
            .ToListAsync();
    }

    public async Task UpdateRelationshipAsync(
        UpdateInfluencerRelationshipDto request)
    {
        var scope = await GetCurrentScopeAsync();

        await EnsureVoterInScopeAsync(scope, request.VoterId);

        var relationship =
            await _dbContext.VoterInfluencers
                .FirstOrDefaultAsync(x =>
                    x.VoterId == request.VoterId &&
                    x.InfluencerId == request.InfluencerId);

        if (relationship == null)
        {
            throw new KeyNotFoundException(
                "Relationship not found.");
        }

        relationship.RelationshipType =
            request.RelationshipType;

        relationship.LinkedAt =
            DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Update Influencer Relationship",
            "Influencer",
            request.InfluencerId.ToString());
    }

    private static System.Linq.Expressions.Expression<Func<Influencer, InfluencerDto>> ToDto() =>
        i => new InfluencerDto
        {
            Id = i.Id,
            NationalId = i.NationalId,
            FullName = i.FullName,
            Address = i.Address,
            ContactNumber = i.ContactNumber,
            ConstituencyId = i.ConstituencyId,
            IslandId = i.IslandId,
            Remarks = i.Remarks,
            ConstituencyName = i.Constituency.Name,
            IslandName = i.Island != null ? i.Island.Name : null,
            LinkedVoters = i.Voters.Count
        };

    public async Task<InfluencerVotersDto> GetLinkedVotersAsync(
        Guid influencerId,
        int page,
        int pageSize,
        LinkedVoterFilter filter)
    {
        var scope = await GetCurrentScopeAsync();

        var influencer = await GetByIdAsync(influencerId);

        page = Math.Max(1, page);
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var allLinks = _dbContext.VoterInfluencers
            .AsNoTracking()
            .Where(vi => vi.InfluencerId == influencerId);

        // The influencer is global, but voters are only shown inside the user's areas.
        var visibleVoterIds = _dbContext.Voters.InScope(scope).Select(v => v.Id);
        var links = allLinks.Where(vi => visibleVoterIds.Contains(vi.VoterId));

        var outside = await allLinks.CountAsync() - await links.CountAsync();

        var relationshipTypes = await links
            .Select(vi => vi.RelationshipType)
            .Where(r => r != "")
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            term = term.Length > 100 ? term[..100] : term;

            links = links.Where(vi =>
                vi.Voter.FullName.Contains(term) ||
                vi.Voter.NationalId.Contains(term) ||
                vi.Voter.MobileNumber.Contains(term) ||
                (vi.Voter.Island != null && vi.Voter.Island.Name.Contains(term)));
        }

        if (filter.ConstituencyId.HasValue)
            links = links.Where(vi => vi.Voter.ConstituencyId == filter.ConstituencyId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim();
            links = links.Where(vi => vi.Voter.SupportStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Relationship))
        {
            var relationship = filter.Relationship.Trim();
            links = links.Where(vi => vi.RelationshipType == relationship);
        }

        var total = await links.CountAsync();

        var items = await links
            .OrderBy(vi => vi.Voter.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(vi => new LinkedVoterDto
            {
                VoterId = vi.VoterId,
                FullName = vi.Voter.FullName,
                NationalId = vi.Voter.NationalId,
                MobileNumber = vi.Voter.MobileNumber,
                Address = vi.Voter.Address,
                IslandName = vi.Voter.Island != null ? vi.Voter.Island.Name : string.Empty,
                ConstituencyName = vi.Voter.Constituency != null ? vi.Voter.Constituency.Name : string.Empty,
                SupportStatus = vi.Voter.SupportStatus,
                RelationshipType = vi.RelationshipType,
                LinkedAt = vi.LinkedAt
            })
            .ToListAsync();

        return new InfluencerVotersDto
        {
            Influencer = influencer,
            OutsideAreaCount = outside,
            RelationshipTypes = relationshipTypes,
            Voters = new HR28.Application.DTOs.Common.PagedResult<LinkedVoterDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            }
        };
    }
}
