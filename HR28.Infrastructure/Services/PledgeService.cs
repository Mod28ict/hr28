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

        // Only these statuses exist; reports count exactly these values.
        var status = AllowedStatuses.FirstOrDefault(s =>
            string.Equals(s, request.Status?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Please choose a valid status.");

        var notes = (request.ResolutionNotes ?? string.Empty).Trim();

        if (notes.Length > 1000)
            throw new BusinessRuleException("Resolution notes must be 1000 characters or fewer.");

        var oldStatus = pledge.Status;
        var notesChanged = notes != (pledge.ResolutionNotes ?? string.Empty);

        if (oldStatus == status && !notesChanged)
            return ToDto(pledge);

        pledge.Status = status;
        pledge.ResolutionNotes = notes;

        // Keep the original completion date if it was already completed.
        if (status == "Completed")
            pledge.FulfilledDate ??= DateTime.UtcNow;
        else
            pledge.FulfilledDate = null;

        await _dbContext.SaveChangesAsync();

        var action = oldStatus == status
            ? "Resolution notes updated"
            : $"Status changed from \"{oldStatus}\" to \"{status}\"" + (notesChanged ? " (notes updated)" : "");

        await _auditService.LogAsync(
            GetCurrentUserId(),
            action,
            "Pledge",
            pledge.Id.ToString());

        return ToDto(pledge);
    }

    public static readonly string[] AllowedStatuses = { "Open", "In Progress", "Completed", "Cancelled" };

    /// <summary>One pledge, if its voter is in the caller's areas (404 otherwise).</summary>
    public async Task<PledgeDto> GetByIdAsync(Guid pledgeId)
    {
        var pledge = await _dbContext.Pledges
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pledgeId)
            ?? throw new KeyNotFoundException("Pledge not found.");

        await EnsureVoterInScopeAsync(pledge.VoterId);

        return ToDto(pledge);
    }

    private static PledgeDto ToDto(Pledge pledge) => new()
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

    public async Task<HR28.Application.DTOs.Common.PagedResult<PledgeListItemDto>> GetListAsync(
        int page,
        int pageSize,
        PledgeListFilter filter)
    {
        var userId = GetCurrentUserId()
            ?? throw new AccessDeniedException("You must be signed in.");

        var scope = await _accessScopeService.GetAsync(userId);

        page = Math.Max(1, page);
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        // Only pledges for voters inside the user's areas.
        var query = _dbContext.Pledges
            .AsNoTracking()
            .InScope(scope, _dbContext.Voters);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            term = term.Length > 100 ? term[..100] : term;

            query = query.Where(p =>
                p.Title.Contains(term) ||
                p.Voter.FullName.Contains(term) ||
                p.Voter.NationalId.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            AllowedStatuses.Contains(filter.Status.Trim()))
        {
            var status = filter.Status.Trim();
            query = query.Where(p => p.Status == status);
        }

        if (filter.OverdueOnly)
        {
            var today = DateTime.Today;

            query = query.Where(p =>
                p.DueDate != null &&
                p.DueDate < today &&
                p.Status != "Completed" &&
                p.Status != "Cancelled");
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.PledgeDate)
            .ThenBy(p => p.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PledgeListItemDto
            {
                Id = p.Id,
                Title = p.Title,
                Status = p.Status,
                Priority = p.Priority,
                PledgeDate = p.PledgeDate,
                DueDate = p.DueDate,
                FulfilledDate = p.FulfilledDate,
                VoterId = p.VoterId,
                VoterName = p.Voter.FullName,
                VoterNationalId = p.Voter.NationalId,
                IslandName = p.Voter.Island != null ? p.Voter.Island.Name : string.Empty,
                ConstituencyName = p.Voter.Constituency != null ? p.Voter.Constituency.Name : string.Empty,
                RecordedBy = p.CreatedByUser.FullName
            })
            .ToListAsync();

        return new HR28.Application.DTOs.Common.PagedResult<PledgeListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
