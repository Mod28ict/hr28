using HR28.Application.Common;
using HR28.Application.DTOs.Pledges;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace HR28.Infrastructure.Services;

public class PledgeService : IPledgeService
{
    private readonly HR28DbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccessScopeService _accessScopeService;

    public PledgeService(
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

    private async Task EnsureVoterInScopeAsync(Guid voterId)
    {
        var userId = GetCurrentUserId()
            ?? throw new AccessDeniedException("You must be signed in.");

        await _dbContext.EnsureVoterInScopeAsync(
            await _accessScopeService.GetAsync(userId),
            voterId);
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

    public async Task<PledgeDto> CreateAsync(
        Guid userId,
        CreatePledgeDto request)
    {
        await EnsureVoterInScopeAsync(request.VoterId);

        var pledge = new Pledge
        {
            Id = Guid.NewGuid(),
            VoterId = request.VoterId,
            CreatedByUserId = userId,
            AssignedToUserId = request.AssignedToUserId,
            PledgeDate = DateTime.UtcNow,
            Title = request.Title,
            Description = request.Description,
            Status = "Open",
            DueDate = request.DueDate,
            ResolutionNotes = string.Empty
        };
        _dbContext.Pledges.Add(pledge);

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            userId,
            "Create",
            "Pledge",
            pledge.Id.ToString());

        return new PledgeDto
        {
            Id = pledge.Id,
            VoterId = pledge.VoterId,
            CreatedByUserId = pledge.CreatedByUserId,
            AssignedToUserId = pledge.AssignedToUserId,
            PledgeDate = pledge.PledgeDate,
            Title = pledge.Title,
            Description = pledge.Description,
            Status = pledge.Status,
            DueDate = pledge.DueDate,
            FulfilledDate = pledge.FulfilledDate,
            ResolutionNotes = pledge.ResolutionNotes
        };
    }

    public async Task<List<PledgeDto>>
        GetByVoterIdAsync(Guid voterId)
    {
        await EnsureVoterInScopeAsync(voterId);

        return await _dbContext.Pledges
            .Where(p => p.VoterId == voterId)
            .OrderByDescending(p => p.PledgeDate)
            .Select(p => new PledgeDto
            {
                Id = p.Id,
                VoterId = p.VoterId,
                CreatedByUserId = p.CreatedByUserId,
                AssignedToUserId = p.AssignedToUserId,
                PledgeDate = p.PledgeDate,
                Title = p.Title,
                Description = p.Description,
                Status = p.Status,
                DueDate = p.DueDate,
                FulfilledDate = p.FulfilledDate,
                ResolutionNotes = p.ResolutionNotes
            })
            .ToListAsync();
    }
    public async Task<PledgeDto> UpdateStatusAsync(
        Guid pledgeId,
        UpdatePledgeStatusDto request)
    {
        var pledge = await _dbContext.Pledges
            .FirstOrDefaultAsync(x => x.Id == pledgeId);

        if (pledge == null)
        {
            throw new KeyNotFoundException("Pledge not found.");
        }

        await EnsureVoterInScopeAsync(pledge.VoterId);

        pledge.Status = request.Status;
        pledge.ResolutionNotes = request.ResolutionNotes;

        if (request.Status == "Completed")
        {
            pledge.FulfilledDate = DateTime.UtcNow;
        }

        if (request.Status != "Completed")
        {
            pledge.FulfilledDate = null;
        }

        await _dbContext.SaveChangesAsync();
        await _auditService.LogAsync(
            GetCurrentUserId(),
            $"Status Changed to {pledge.Status}",
            "Pledge",
            pledge.Id.ToString());

        return new PledgeDto
        {
            Id = pledge.Id,
            VoterId = pledge.VoterId,
            CreatedByUserId = pledge.CreatedByUserId,
            AssignedToUserId = pledge.AssignedToUserId,
            PledgeDate = pledge.PledgeDate,
            Title = pledge.Title,
            Description = pledge.Description,
            Status = pledge.Status,
            DueDate = pledge.DueDate,
            FulfilledDate = pledge.FulfilledDate,
            ResolutionNotes = pledge.ResolutionNotes
        };
    }

}