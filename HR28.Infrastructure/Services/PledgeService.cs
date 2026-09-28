using HR28.Application.DTOs.Pledges;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class PledgeService : IPledgeService
{
    private readonly HR28DbContext _dbContext;

    public PledgeService(
        HR28DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PledgeDto> CreateAsync(
        Guid userId,
        CreatePledgeDto request)
    {
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
            throw new Exception("Pledge not found.");
        }

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