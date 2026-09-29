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
    private readonly IInfluencerService _influencerService;

    public InfluencerService(
        HR28DbContext dbContext,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
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

        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Create",
            "Influencer",
            influencer.Id.ToString());


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
    public async Task UpdateRelationshipAsync(
        UpdateInfluencerRelationshipDto request)
    {
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

}