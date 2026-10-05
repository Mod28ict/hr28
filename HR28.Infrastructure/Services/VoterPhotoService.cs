using HR28.Application.Common;
using HR28.Application.DTOs.Access;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using HR28.Infrastructure.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class VoterPhotoService : IVoterPhotoService
{
    public const int MaxBytes = 2 * 1024 * 1024;

    private readonly HR28DbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccessScopeService _accessScopeService;

    public VoterPhotoService(
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

    private async Task<(Guid UserId, AccessScope Scope)> RequireAsync(string permission, Guid voterId)
    {
        var userId = Guid.TryParse(
            _httpContextAccessor.HttpContext?.User
                .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var id)
            ? id
            : throw new AccessDeniedException("You must be signed in.");

        var scope = await _accessScopeService.GetAsync(userId);

        if (!scope.HasPermission(permission))
            throw new AccessDeniedException("You don't have permission to do that with voter photos. Ask your Administrator.");

        // Outside the user's areas looks the same as a voter that doesn't exist.
        await _dbContext.EnsureVoterInScopeAsync(scope, voterId);

        return (userId, scope);
    }

    public async Task<(byte[] Content, string ContentType)?> GetAsync(Guid voterId)
    {
        await RequireAsync(PermissionCatalog.VotersPhotoView, voterId);

        var photo = await _dbContext.VoterPhotos
            .AsNoTracking()
            .Where(p => p.VoterId == voterId)
            .Select(p => new { p.Content, p.ContentType })
            .FirstOrDefaultAsync();

        return photo == null ? null : (photo.Content, photo.ContentType);
    }

    public async Task SaveAsync(Guid voterId, byte[] data)
    {
        var (userId, _) = await RequireAsync(PermissionCatalog.VotersPhotoEdit, voterId);

        if (data.Length == 0)
            throw new BusinessRuleException("Please choose a photo.");

        if (data.Length > MaxBytes)
            throw new BusinessRuleException("The photo is too large. Please choose one under 2 MB.");

        var (content, contentType, error) = PhotoSanitizer.Clean(data);

        if (content == null || contentType == null)
            throw new BusinessRuleException(error ?? "That photo could not be read.");

        var photo = await _dbContext.VoterPhotos.FirstOrDefaultAsync(p => p.VoterId == voterId);
        var replacing = photo != null;

        if (photo == null)
        {
            photo = new VoterPhoto { VoterId = voterId };
            _dbContext.VoterPhotos.Add(photo);
        }

        photo.Content = content;
        photo.ContentType = contentType;
        photo.SizeBytes = content.Length;
        photo.UploadedAt = DateTime.UtcNow;
        photo.UploadedByUserId = userId;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            userId,
            replacing ? "Photo replaced" : "Photo added",
            "Voter",
            voterId.ToString());
    }

    public async Task RemoveAsync(Guid voterId)
    {
        var (userId, _) = await RequireAsync(PermissionCatalog.VotersPhotoEdit, voterId);

        var photo = await _dbContext.VoterPhotos.FirstOrDefaultAsync(p => p.VoterId == voterId);

        if (photo == null)
            return;

        _dbContext.VoterPhotos.Remove(photo);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(userId, "Photo removed", "Voter", voterId.ToString());
    }
}
