using HR28.Application.Common;
using HR28.Application.DTOs.Influencers;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

/// <summary>
/// Influencer categories (MP, Island Council, GM Member, …). Everyone can read the
/// list; the API lets only administrators change it. Every change is audited.
/// </summary>
public class InfluencerCategoryService : IInfluencerCategoryService
{
    private const string AuditEntity = "Influencer category";

    private readonly HR28DbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InfluencerCategoryService(
        HR28DbContext dbContext,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(
            _httpContextAccessor.HttpContext?.User
                .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var id)
            ? id
            : null;

    public async Task<List<InfluencerCategoryDto>> GetAllAsync()
    {
        return await _dbContext.InfluencerCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new InfluencerCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                SortOrder = c.SortOrder,
                InfluencerCount = _dbContext.Influencers.Count(i => i.CategoryId == c.Id)
            })
            .ToListAsync();
    }

    private async Task<string> ValidateNameAsync(string? name, Guid? existingId)
    {
        var clean = string.Join(' ', (name ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (clean.Length == 0)
            throw new BusinessRuleException("Please enter a category name.");

        if (clean.Length > 60)
            throw new BusinessRuleException("Category names can be at most 60 characters.");

        // SQL Server compares names case-insensitively, so "mp" matches "MP".
        var existing = await _dbContext.InfluencerCategories
            .Where(c => c.Name == clean && c.Id != existingId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync();

        if (existing != null)
            throw new BusinessRuleException($"There is already a category called \"{existing}\".");

        return clean;
    }

    public async Task<InfluencerCategoryDto> CreateAsync(SaveInfluencerCategoryDto request)
    {
        var name = await ValidateNameAsync(request.Name, null);

        var nextOrder = (await _dbContext.InfluencerCategories
            .MaxAsync(c => (int?)c.SortOrder) ?? 0) + 1;

        var category = new InfluencerCategory
        {
            Id = Guid.NewGuid(),
            Name = name,
            SortOrder = nextOrder,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.InfluencerCategories.Add(category);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(CurrentUserId, "Create", AuditEntity, name);

        return new InfluencerCategoryDto { Id = category.Id, Name = name, SortOrder = nextOrder };
    }

    public async Task<InfluencerCategoryDto> UpdateAsync(Guid id, SaveInfluencerCategoryDto request)
    {
        var category = await _dbContext.InfluencerCategories.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("Category not found.");

        var name = await ValidateNameAsync(request.Name, id);
        var oldName = category.Name;

        if (oldName != name)
        {
            category.Name = name;
            await _dbContext.SaveChangesAsync();

            await _auditService.LogAsync(CurrentUserId, $"Rename: \"{oldName}\" → \"{name}\"", AuditEntity, name);
        }

        return new InfluencerCategoryDto { Id = id, Name = name, SortOrder = category.SortOrder };
    }

    public async Task<string> DeleteAsync(Guid id)
    {
        var category = await _dbContext.InfluencerCategories.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("Category not found.");

        var inUse = await _dbContext.Influencers.CountAsync(i => i.CategoryId == id);

        if (inUse > 0)
            throw new BusinessRuleException(
                $"\"{category.Name}\" is used by {inUse} influencer{(inUse == 1 ? "" : "s")}. " +
                "Change their category first, or rename this one instead.");

        _dbContext.InfluencerCategories.Remove(category);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(CurrentUserId, "Delete", AuditEntity, category.Name);

        return category.Name;
    }
}
