using HR28.Application.DTOs.Influencers;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class InfluencerService : IInfluencerService
{
    private readonly HR28DbContext _dbContext;

    public InfluencerService(
        HR28DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InfluencerDto> CreateAsync(
        CreateInfluencerDto request)
    {
        var influencer = new Influencer
        {
            Id = Guid.NewGuid(),
            NationalId = request.NationalId,
            FullName = request.FullName,
            Address = request.Address,
            ContactNumber = request.ContactNumber,
            ConstituencyId = request.ConstituencyId,
            IslandId = request.IslandId,
            Remarks = request.Remarks,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Influencers.Add(influencer);

        await _dbContext.SaveChangesAsync();

        return new InfluencerDto
        {
            Id = influencer.Id,
            NationalId = influencer.NationalId,
            FullName = influencer.FullName,
            Address = influencer.Address,
            ContactNumber = influencer.ContactNumber,
            ConstituencyId = influencer.ConstituencyId,
            IslandId = influencer.IslandId,
            Remarks = influencer.Remarks
        };
    }

    public async Task<List<InfluencerDto>> GetAllAsync()
    {
        return await _dbContext.Influencers
            .Select(i => new InfluencerDto
            {
                Id = i.Id,
                NationalId = i.NationalId,
                FullName = i.FullName,
                Address = i.Address,
                ContactNumber = i.ContactNumber,
                ConstituencyId = i.ConstituencyId,
                IslandId = i.IslandId,
                Remarks = i.Remarks
            })
            .ToListAsync();
    }

    public async Task LinkToVoterAsync(
        LinkInfluencerDto request)
    {
        var link = new VoterInfluencer
        {
            Id = Guid.NewGuid(),
            VoterId = request.VoterId,
            InfluencerId = request.InfluencerId,
            RelationshipType = request.RelationshipType,
            LinkedAt = DateTime.UtcNow
        };

        _dbContext.VoterInfluencers.Add(link);

        await _dbContext.SaveChangesAsync();
    }
    public async Task<List<VoterInfluencerDto>>
        GetByVoterIdAsync(Guid voterId)
    {
        var links = await _dbContext.VoterInfluencers
            .Where(x => x.VoterId == voterId)
            .ToListAsync();

        var result = new List<VoterInfluencerDto>();

        foreach (var link in links)
        {
            var influencer = await _dbContext.Influencers
                .FirstOrDefaultAsync(i =>
                    i.Id == link.InfluencerId);

            if (influencer == null)
                continue;

            result.Add(new VoterInfluencerDto
            {
                InfluencerId = influencer.Id,
                FullName = influencer.FullName,
                NationalId = influencer.NationalId,
                ContactNumber = influencer.ContactNumber,
                RelationshipType = link.RelationshipType
            });
        }

        return result;
    }
}