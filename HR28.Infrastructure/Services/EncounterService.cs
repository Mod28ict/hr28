using HR28.Application.DTOs.Access;
using HR28.Application.DTOs.Encounters;
using HR28.Application.Common;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class EncounterService : IEncounterService
{
    private readonly HR28DbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccessScopeService _accessScopeService;

    public EncounterService(
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

    public async Task<EncounterDto> CreateAsync(
        Guid userId,
        CreateEncounterDto request)
    {
        var scope = await _accessScopeService.GetAsync(userId);

        await _dbContext.EnsureVoterInScopeAsync(scope, request.VoterId);

        var type = request.EncounterType?.Trim() ?? string.Empty;
        var outcome = request.Outcome?.Trim() ?? string.Empty;
        // Only the Administrator and roles given "Set encounter response" record the response;
        // everyone else's encounters start with no response.
        var response = scope.HasPermission(PermissionCatalog.EncountersResponse) && !string.IsNullOrWhiteSpace(request.Response)
            ? request.Response.Trim()
            : null;
        var notes = request.Notes?.Trim() ?? string.Empty;

        if (!EncounterTypes.Contains(type))
            throw new BusinessRuleException("Choose an encounter type from the list.");

        if (!Outcomes.Contains(outcome))
            throw new BusinessRuleException("Choose Meet, Call or Request.");

        if (response != null && !Responses.Contains(response))
            throw new BusinessRuleException("Choose the voter's response: Supports, Undecided or Does not support.");

        if (notes.Length > MaxNotesLength)
            throw new BusinessRuleException($"Notes can be at most {MaxNotesLength} characters.");

        if (request.EncounterDate == default || request.EncounterDate > MaldivesTime.Now.AddDays(1))
            throw new BusinessRuleException("Enter the date the encounter happened (not in the future).");

        var encounter = new Encounter
        {
            Id = Guid.NewGuid(),
            VoterId = request.VoterId,
            RecordedByUserId = userId,
            EncounterDate = request.EncounterDate,
            EncounterType = type,
            Outcome = outcome,
            Response = response,
            Notes = notes
        };

        _dbContext.Encounters.Add(encounter);

        await _dbContext.SaveChangesAsync();
        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Create",
            "Encounter",
            encounter.Id.ToString());



        return new EncounterDto
        {
            Id = encounter.Id,
            VoterId = encounter.VoterId,
            RecordedByUserId = encounter.RecordedByUserId,
            EncounterDate = encounter.EncounterDate,
            EncounterType = encounter.EncounterType,
            Outcome = encounter.Outcome,
            Response = encounter.Response,
            Notes = encounter.Notes
        };
    }

    public async Task<List<EncounterDto>>
        GetByVoterIdAsync(Guid voterId)
    {
        var userId = GetCurrentUserId()
            ?? throw new AccessDeniedException("You must be signed in.");

        await _dbContext.EnsureVoterInScopeAsync(
            await _accessScopeService.GetAsync(userId),
            voterId);

        return await _dbContext.Encounters
            .Where(e => e.VoterId == voterId)
            .OrderByDescending(e => e.EncounterDate)
            .Select(e => new EncounterDto
            {
                Id = e.Id,
                VoterId = e.VoterId,
                RecordedByUserId = e.RecordedByUserId,
                EncounterDate = e.EncounterDate,
                EncounterType = e.EncounterType,
                Outcome = e.Outcome,
                Response = e.Response,
                Notes = e.Notes
            })
            .ToListAsync();
    }

    private static readonly string[] EncounterTypes = EncounterValues.Types;
    private static readonly string[] Outcomes = EncounterValues.Outcomes;
    private static readonly string[] Responses = EncounterValues.Responses;

    public const int MaxNotesLength = 1000;

    private static EncounterDto ToDto(Encounter e) => new()
    {
        Id = e.Id,
        VoterId = e.VoterId,
        RecordedByUserId = e.RecordedByUserId,
        EncounterDate = e.EncounterDate,
        EncounterType = e.EncounterType,
        Outcome = e.Outcome,
        Response = e.Response,
        Notes = e.Notes
    };

    /// <summary>The encounter, only if its voter is inside the user's areas (404 otherwise).</summary>
    private async Task<Encounter> FindInScopeAsync(AccessScope scope, Guid id)
    {
        // Outside the user's areas gives the same answer as "not found", so nothing is revealed.
        return await _dbContext.Encounters
            .InScope(scope, _dbContext.Voters)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Encounter not found.");
    }

    public async Task<EncounterDto> GetByIdAsync(Guid id)
    {
        var userId = GetCurrentUserId()
            ?? throw new AccessDeniedException("You must be signed in.");

        var scope = await _accessScopeService.GetAsync(userId);

        return ToDto(await FindInScopeAsync(scope, id));
    }

    public async Task<EncounterDto> UpdateAsync(Guid id, UpdateEncounterDto request)
    {
        var userId = GetCurrentUserId()
            ?? throw new AccessDeniedException("You must be signed in.");

        var scope = await _accessScopeService.GetAsync(userId);

        if (!scope.HasPermission(PermissionCatalog.EncountersEdit))
            throw new AccessDeniedException("You don't have permission to edit encounters. Ask your Administrator.");

        var encounter = await FindInScopeAsync(scope, id);

        var type = request.EncounterType?.Trim() ?? string.Empty;
        var outcome = request.Outcome?.Trim() ?? string.Empty;
        // Without "Set encounter response" the response stays as it is, whatever is sent.
        var response = !scope.HasPermission(PermissionCatalog.EncountersResponse)
            ? encounter.Response
            : string.IsNullOrWhiteSpace(request.Response) ? null : request.Response.Trim();
        var notes = request.Notes?.Trim() ?? string.Empty;

        // Older records may hold a value no longer offered; keeping it unchanged is allowed.
        if (type != encounter.EncounterType && !EncounterTypes.Contains(type))
            throw new BusinessRuleException("Choose an encounter type from the list.");

        if (outcome != encounter.Outcome && !Outcomes.Contains(outcome))
            throw new BusinessRuleException("Choose Meet, Call or Request.");

        if (response != encounter.Response && !Responses.Contains(response))
            throw new BusinessRuleException("Choose the voter's response: Supports, Undecided or Does not support.");

        if (notes.Length > MaxNotesLength)
            throw new BusinessRuleException($"Notes can be at most {MaxNotesLength} characters.");

        if (request.EncounterDate == default || request.EncounterDate > MaldivesTime.Now.AddDays(1))
            throw new BusinessRuleException("Enter the date the encounter happened (not in the future).");

        // Record which fields changed; notes are free text, so only that they changed.
        var changes = new List<string>();

        if (encounter.EncounterDate != request.EncounterDate)
            changes.Add($"date {encounter.EncounterDate:dd MMM yyyy HH:mm} → {request.EncounterDate:dd MMM yyyy HH:mm}");
        if (encounter.EncounterType != type) changes.Add($"type \"{encounter.EncounterType}\" → \"{type}\"");
        if (encounter.Outcome != outcome) changes.Add($"outcome \"{encounter.Outcome}\" → \"{outcome}\"");
        if (encounter.Response != response) changes.Add($"response \"{encounter.Response ?? "none"}\" → \"{response ?? "none"}\"");
        if (encounter.Notes != notes) changes.Add("notes");

        if (changes.Count == 0)
            return ToDto(encounter);

        encounter.EncounterDate = request.EncounterDate;
        encounter.EncounterType = type;
        encounter.Outcome = outcome;
        encounter.Response = response;
        encounter.Notes = notes;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            userId,
            "Update: " + string.Join(", ", changes),
            "Encounter",
            id.ToString());

        return ToDto(encounter);
    }

    public async Task<string?> SetResponseAsync(Guid id, string? response)
    {
        var userId = GetCurrentUserId()
            ?? throw new AccessDeniedException("You must be signed in.");

        var scope = await _accessScopeService.GetAsync(userId);

        if (!scope.HasPermission(PermissionCatalog.EncountersResponse))
            throw new AccessDeniedException("You don't have permission to set encounter responses. Ask your Administrator.");

        var encounter = await FindInScopeAsync(scope, id);

        var value = string.IsNullOrWhiteSpace(response) ? null : response.Trim();

        if (value != null && !Responses.Contains(value))
            throw new BusinessRuleException("Choose the voter's response: Supports, Undecided or Does not support.");

        if (encounter.Response == value)
            return value;

        var old = encounter.Response ?? "none";
        encounter.Response = value;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(userId, $"Response: \"{old}\" → \"{value ?? "none"}\"", "Encounter", id.ToString());

        return value;
    }

    public async Task DeleteAsync(Guid id)
    {
        var userId = GetCurrentUserId()
            ?? throw new AccessDeniedException("You must be signed in.");

        var scope = await _accessScopeService.GetAsync(userId);
        var encounter = await FindInScopeAsync(scope, id);

        var voter = await _dbContext.Voters
            .Where(v => v.Id == encounter.VoterId)
            .Select(v => new { v.FullName, v.NationalId })
            .FirstAsync();

        var label = $"{encounter.Outcome} on {encounter.EncounterDate:dd MMM yyyy} — {voter.FullName} ({voter.NationalId})";

        _dbContext.Encounters.Remove(encounter);
        await _dbContext.SaveChangesAsync();

        // The record is gone, so the audit entry carries what it was (notes are not copied).
        await _auditService.LogAsync(
            userId,
            $"Delete (permanent): {label}",
            "Encounter",
            id.ToString());
    }

    public async Task<HR28.Application.DTOs.Common.PagedResult<EncounterListItemDto>> GetListAsync(
        int page,
        int pageSize,
        EncounterListFilter filter)
    {
        var userId = GetCurrentUserId()
            ?? throw new AccessDeniedException("You must be signed in.");

        var scope = await _accessScopeService.GetAsync(userId);

        page = Math.Max(1, page);
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        // Only encounters for voters inside the user's areas.
        var query = _dbContext.Encounters
            .AsNoTracking()
            .InScope(scope, _dbContext.Voters);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            term = term.Length > 100 ? term[..100] : term;

            query = query.Where(e =>
                e.Voter.FullName.Contains(term) ||
                e.Voter.NationalId.Contains(term) ||
                e.Notes.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.EncounterType) && EncounterTypes.Contains(filter.EncounterType.Trim()))
        {
            var type = filter.EncounterType.Trim();
            query = query.Where(e => e.EncounterType == type);
        }

        if (!string.IsNullOrWhiteSpace(filter.Outcome) && Outcomes.Contains(filter.Outcome.Trim()))
        {
            var outcome = filter.Outcome.Trim();
            query = query.Where(e => e.Outcome == outcome);
        }

        if (!string.IsNullOrWhiteSpace(filter.Response) && Responses.Contains(filter.Response.Trim()))
        {
            var response = filter.Response.Trim();
            query = query.Where(e => e.Response == response);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.EncounterDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EncounterListItemDto
            {
                Id = e.Id,
                EncounterDate = e.EncounterDate,
                EncounterType = e.EncounterType,
                Outcome = e.Outcome,
                Response = e.Response,
                Notes = e.Notes,
                VoterId = e.VoterId,
                VoterName = e.Voter.FullName,
                VoterNationalId = e.Voter.NationalId,
                IslandName = e.Voter.Island != null ? e.Voter.Island.Name : string.Empty,
                ConstituencyName = e.Voter.Constituency != null ? e.Voter.Constituency.Name : string.Empty,
                RecordedBy = e.RecordedByUser.FullName
            })
            .ToListAsync();

        return new HR28.Application.DTOs.Common.PagedResult<EncounterListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
