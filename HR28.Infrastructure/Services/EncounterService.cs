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

        var encounter = new Encounter
        {
            Id = Guid.NewGuid(),
            VoterId = request.VoterId,
            RecordedByUserId = userId,
            EncounterDate = request.EncounterDate,
            EncounterType = request.EncounterType,
            Outcome = request.Outcome,
            Notes = request.Notes
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
                Notes = e.Notes
            })
            .ToListAsync();
    }

    public static readonly string[] EncounterTypes =
        { "Door Visit", "Phone Call", "Meeting", "Campaign Event", "Office Visit", "Other" };

    public static readonly string[] Outcomes =
        { "Positive", "Undecided", "Negative", "Follow-up Required", "No Contact" };

    public const int MaxNotesLength = 1000;

    private static EncounterDto ToDto(Encounter e) => new()
    {
        Id = e.Id,
        VoterId = e.VoterId,
        RecordedByUserId = e.RecordedByUserId,
        EncounterDate = e.EncounterDate,
        EncounterType = e.EncounterType,
        Outcome = e.Outcome,
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
        var notes = request.Notes?.Trim() ?? string.Empty;

        // Older records may hold a value no longer offered; keeping it unchanged is allowed.
        if (type != encounter.EncounterType && !EncounterTypes.Contains(type))
            throw new BusinessRuleException("Choose an encounter type from the list.");

        if (outcome != encounter.Outcome && !Outcomes.Contains(outcome))
            throw new BusinessRuleException("Choose an outcome from the list.");

        if (notes.Length > MaxNotesLength)
            throw new BusinessRuleException($"Notes can be at most {MaxNotesLength} characters.");

        if (request.EncounterDate == default || request.EncounterDate > DateTime.Now.AddDays(1))
            throw new BusinessRuleException("Enter the date the encounter happened (not in the future).");

        // Record which fields changed; notes are free text, so only that they changed.
        var changes = new List<string>();

        if (encounter.EncounterDate != request.EncounterDate)
            changes.Add($"date {encounter.EncounterDate:dd MMM yyyy HH:mm} → {request.EncounterDate:dd MMM yyyy HH:mm}");
        if (encounter.EncounterType != type) changes.Add($"type \"{encounter.EncounterType}\" → \"{type}\"");
        if (encounter.Outcome != outcome) changes.Add($"outcome \"{encounter.Outcome}\" → \"{outcome}\"");
        if (encounter.Notes != notes) changes.Add("notes");

        if (changes.Count == 0)
            return ToDto(encounter);

        encounter.EncounterDate = request.EncounterDate;
        encounter.EncounterType = type;
        encounter.Outcome = outcome;
        encounter.Notes = notes;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            userId,
            "Update: " + string.Join(", ", changes),
            "Encounter",
            id.ToString());

        return ToDto(encounter);
    }
}
