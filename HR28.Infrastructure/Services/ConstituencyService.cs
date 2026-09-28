using HR28.Application.DTOs.Constituencies;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class ConstituencyService : IConstituencyService
{
    private readonly HR28DbContext _context;

    public ConstituencyService(HR28DbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ConstituencyDto>> GetAllAsync()
    {
        return await _context.Constituencies
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .ThenBy(c => c.Name)
            .Select(c => new ConstituencyDto
            {
                Id = c.Id,
                Code = c.Code ?? string.Empty,
                Name = c.Name
            })
            .ToListAsync();
    }

    public async Task<ConstituencyDto?> GetByIdAsync(Guid id)
    {
        return await _context.Constituencies
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ConstituencyDto
            {
                Id = c.Id,
                Code = c.Code ?? string.Empty,
                Name = c.Name
            })
            .FirstOrDefaultAsync();
    }

    public async Task<ConstituencyDto> CreateAsync(
        CreateConstituencyDto dto)
    {
        var code = dto.Code.Trim();
        var name = dto.Name.Trim();

        var duplicateExists = await _context.Constituencies
            .AnyAsync(c =>
                (c.Code != null && c.Code == code) ||
                c.Name == name);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "A constituency with the same code or name already exists.");
        }

        var constituency = new Constituency
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name
        };

        _context.Constituencies.Add(constituency);
        await _context.SaveChangesAsync();

        return new ConstituencyDto
        {
            Id = constituency.Id,
            Code = constituency.Code ?? string.Empty,
            Name = constituency.Name
        };
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateConstituencyDto dto)
    {
        var constituency = await _context.Constituencies
            .FirstOrDefaultAsync(c => c.Id == id);

        if (constituency is null)
            return false;

        var code = dto.Code.Trim();
        var name = dto.Name.Trim();

        var duplicateExists = await _context.Constituencies
            .AnyAsync(c =>
                c.Id != id &&
                ((c.Code != null && c.Code == code) ||
                 c.Name == name));

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "Another constituency with the same code or name already exists.");
        }

        constituency.Code = code;
        constituency.Name = name;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var constituency = await _context.Constituencies
            .FirstOrDefaultAsync(c => c.Id == id);

        if (constituency is null)
            return false;

        var isInUse =
            await _context.Islands
                .AnyAsync(i => i.ConstituencyId == id) ||
            await _context.Set<ConstituencyIsland>()
                .AnyAsync(ci => ci.ConstituencyId == id);

        if (isInUse)
        {
            throw new InvalidOperationException(
                "The constituency cannot be deleted because it is linked to one or more islands.");
        }

        _context.Constituencies.Remove(constituency);
        await _context.SaveChangesAsync();

        return true;
    }
}