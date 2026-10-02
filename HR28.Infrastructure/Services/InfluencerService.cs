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

    private async Task EnsureInfluencerInScopeAsync(AccessScope scope, Guid influencerId)
    {
        var visible = await _dbContext.Influencers
            .InScope(scope)
            .AnyAsync(i => i.Id == influencerId);

        if (!visible)
            throw new KeyNotFoundException();
    }

    /// <summary>Checks shared by create and edit. Returns the cleaned National ID and name.</summary>
    private async Task<(string NationalId, string FullName)> ValidateAsync(
        AccessScope scope,
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

        if (!scope.Allows(request.ConstituencyId, request.IslandId))
            throw new AccessDeniedException("You can only place influencers within your assigned area.");

        var duplicate = await _dbContext.Influencers
            .AnyAsync(i => i.NationalId == nationalId && i.Id != existingId);

        if (duplicate)
            throw new BusinessRuleException($"An influencer with National ID {nationalId} already exists.");

        return (nationalId, fullName);
    }

    public async Task<InfluencerDto> CreateAsync(
        CreateInfluencerDto request)
    {
        var scope = await GetCurrentScopeAsync();

        var (nationalId, fullName) = await ValidateAsync(scope, request, null);

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
        var scope = await GetCurrentScopeAsync();

        return await _dbContext.Influencers
            .AsNoTracking()
            .InScope(scope)
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

        // Must currently be in the user's areas (404 otherwise)...
        var influencer = await _dbContext.Influencers
            .InScope(scope)
            .FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new KeyNotFoundException("Influencer not found.");

        // ...and the new area must be too.
        var (nationalId, fullName) = await ValidateAsync(scope, request, id);

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
            .InScope(scope)
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
        var scope = await GetCurrentScopeAsync();

        return await _dbContext.Influencers
            .AsNoTracking()
            .InScope(scope)
            .OrderBy(i => i.FullName)
            .Select(ToDto())
            .ToListAsync();
    }

    public async Task LinkToVoterAsync(
        LinkInfluencerDto request)
    {
        var scope = await GetCurrentScopeAsync();

        await EnsureVoterInScopeAsync(scope, request.VoterId);
        await EnsureInfluencerInScopeAsync(scope, request.InfluencerId);

        var existingLink =
            await _dbContext.VoterInfluencers
                .FirstOrDefaultAsync(x =>
                    x.VoterId == request.VoterId &&
                    x.InfluencerId == request.InfluencerId);

        string auditAction;

        if (existingLink == null)
        {
            var newLink = new VoterInfluencer
            {
                Id = Guid.NewGuid(),
                VoterId = request.VoterId,
                InfluencerId = request.InfluencerId,
                RelationshipType =
                    request.RelationshipType,
                LinkedAt = DateTime.UtcNow
            };

            _dbContext.VoterInfluencers.Add(
                newLink);

            auditAction = "Link To Voter";
        }
        else
        {
            existingLink.RelationshipType =
                request.RelationshipType;

            // Treat this timestamp as the latest link update.
            existingLink.LinkedAt =
                DateTime.UtcNow;

            auditAction =
                "Update Voter Relationship";
        }

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
}
