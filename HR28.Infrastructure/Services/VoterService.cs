using HR28.Application.DTOs.Voters;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace HR28.Infrastructure.Services;

public class VoterService : IVoterService
{
    private readonly HR28DbContext _dbContext;
    private async Task<bool> HasFullAccessAsync(Guid userId)
    {
        return await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .AnyAsync(ur =>
                ur.UserId == userId &&
                (
                    ur.Role.Name == "Super Administrator" ||
                    ur.Role.Name == "National Administrator"
                ));
    }


    public VoterService(HR28DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<VoterDto> CreateVoterAsync(
        CreateVoterDto request)
    {
        var voter = new Voter
        {
            Id = Guid.NewGuid(),
            NationalId = request.NationalId,
            FullName = request.FullName,
            Address = request.Address,
            MobileNumber = request.MobileNumber,
            ConstituencyId = request.ConstituencyId,
            IslandId = request.IslandId,
            Remarks = request.Remarks,
            CreatedAt = DateTime.UtcNow,
            SupportStatus = request.SupportStatus 
        };

        _dbContext.Voters.Add(voter);

        await _dbContext.SaveChangesAsync();

        return new VoterDto
        {
            Id = voter.Id,
            NationalId = voter.NationalId,
            FullName = voter.FullName,
            Address = voter.Address,
            MobileNumber = voter.MobileNumber,
            ConstituencyId = voter.ConstituencyId,
            IslandId = voter.IslandId,
            Remarks = voter.Remarks
        };
    }

    public async Task<List<VoterDto>> GetVotersAsync(Guid userId)
    {
        var userScopes = await _dbContext.UserScopes
            .Where(x => x.UserId == userId)
            .ToListAsync();
        var hasFullAccess =
            await HasFullAccessAsync(userId);

        var query = _dbContext.Voters.AsQueryable();

        if (!hasFullAccess)
        {
            if (!userScopes.Any())
            {
                return new List<VoterDto>();
            }

            var constituencyIds = userScopes
                .Where(x => x.ConstituencyId.HasValue)
                .Select(x => x.ConstituencyId!.Value)
                .ToList();

            var islandIds = userScopes
                .Where(x => x.IslandId.HasValue)
                .Select(x => x.IslandId!.Value)
                .ToList();

            query = query.Where(v =>
                constituencyIds.Contains(v.ConstituencyId) ||
                (v.IslandId.HasValue &&
                 islandIds.Contains(v.IslandId.Value)));
        }

        return await query
            .Select(v => new VoterDto
            {
                Id = v.Id,
                NationalId = v.NationalId,
                FullName = v.FullName,
                Address = v.Address,
                MobileNumber = v.MobileNumber,
                ConstituencyId = v.ConstituencyId,
                IslandId = v.IslandId,
                Remarks = v.Remarks,
                SupportStatus = v.SupportStatus
            })
            .ToListAsync();
    }

    public async Task<VoterDto?> GetVoterByIdAsync(Guid id)
    {
        return await _dbContext.Voters
            .Where(v => v.Id == id)
            .Select(v => new VoterDto
            {
                Id = v.Id,
                NationalId = v.NationalId,
                FullName = v.FullName,
                Address = v.Address,
                MobileNumber = v.MobileNumber,
                ConstituencyId = v.ConstituencyId,
                IslandId = v.IslandId,
                Remarks = v.Remarks,
                SupportStatus = v.SupportStatus
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<VoterDto>> SearchVotersAsync(
        string searchTerm)
    {
        return await _dbContext.Voters
            .Where(v =>
                v.FullName.Contains(searchTerm) ||
                v.NationalId.Contains(searchTerm))
            .Select(v => new VoterDto
            {
                Id = v.Id,
                NationalId = v.NationalId,
                FullName = v.FullName,
                Address = v.Address,
                MobileNumber = v.MobileNumber,
                ConstituencyId = v.ConstituencyId,
                IslandId = v.IslandId,
                Remarks = v.Remarks
            })
            .ToListAsync();
    }
    public async Task UpdateVoterAsync(
    Guid id,
    UpdateVoterDto request)
    {
        var voter = await _dbContext.Voters
            .FirstOrDefaultAsync(v => v.Id == id);

        if (voter == null)
            throw new Exception("Voter not found.");

        voter.NationalId = request.NationalId;
        voter.FullName = request.FullName;
        voter.Address = request.Address;
        voter.MobileNumber = request.MobileNumber;
        voter.ConstituencyId = request.ConstituencyId;
        voter.IslandId = request.IslandId;
        voter.SupportStatus = request.SupportStatus;
        voter.Remarks = request.Remarks;

        await _dbContext.SaveChangesAsync();
    }
    public async Task DeleteVoterAsync(Guid id)
    {
        var voter = await _dbContext.Voters
            .FirstOrDefaultAsync(v => v.Id == id);

        if (voter == null)
            throw new Exception("Voter not found.");

        _dbContext.Voters.Remove(voter);

        await _dbContext.SaveChangesAsync();
    }



}