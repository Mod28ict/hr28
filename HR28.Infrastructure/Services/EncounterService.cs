using HR28.Application.DTOs.Encounters;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class EncounterService : IEncounterService
{
    private readonly HR28DbContext _dbContext;

    public EncounterService(
        HR28DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EncounterDto> CreateAsync(
        Guid userId,
        CreateEncounterDto request)
    {
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