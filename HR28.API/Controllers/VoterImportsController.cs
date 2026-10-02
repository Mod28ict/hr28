using HR28.API.Extensions;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HR28.API.Controllers;

/// <summary>
/// Bulk voter upload from the voter list spreadsheet. Administrators only
/// (Administrator and National Administrator), because an import can add or
/// change voters and constituencies anywhere. Every upload is audited.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.Administrator)]
public class VoterImportsController : ControllerBase
{
    public const long MaxFileBytes = 20 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".xlsx", ".xls" };

    private readonly IVoterImportService _voterImportService;

    public VoterImportsController(
        IVoterImportService voterImportService)
    {
        _voterImportService = voterImportService;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Import)]
    [RequestSizeLimit(MaxFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> Import(
        IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Choose an Excel file to upload." });

        if (file.Length > MaxFileBytes)
            return BadRequest(new { message = "The file is larger than 20 MB. Split it into smaller files and upload them one at a time." });

        var extension = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new { message = "Only Excel files (.xlsx or .xls) can be uploaded." });

        await using var stream = file.OpenReadStream();

        var result = await _voterImportService.ImportAsync(stream, file.FileName ?? string.Empty);

        return Ok(result);
    }
}
