using HR28.API.Extensions;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HR28.API.Controllers;

/// <summary>
/// Party membership list upload: matches voters by National ID, sets their party and
/// fills empty date of birth, mobile and gender. Administrators only (it changes voters
/// everywhere); shares the voter upload limit (10 per hour). Every upload is audited.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.Administrator)]
public class MembershipImportsController : ControllerBase
{
    public const long MaxFileBytes = 20 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".xlsx", ".xls" };

    private readonly IPartyMembershipImportService _service;

    public MembershipImportsController(IPartyMembershipImportService service)
    {
        _service = service;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Import)]
    [RequestSizeLimit(MaxFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> Import([FromQuery] Guid partyId, IFormFile? file)
    {
        if (partyId == Guid.Empty)
            return BadRequest(new { message = "Please choose the party the list belongs to." });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Choose an Excel file to upload." });

        if (file.Length > MaxFileBytes)
            return BadRequest(new { message = "The file is larger than 20 MB. Split it into smaller files and upload them one at a time." });

        if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant()))
            return BadRequest(new { message = "Only Excel files (.xlsx or .xls) can be uploaded." });

        await using var stream = file.OpenReadStream();

        return Ok(await _service.ImportAsync(stream, file.FileName ?? string.Empty, partyId));
    }
}
