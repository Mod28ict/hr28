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
}