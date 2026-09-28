using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VoterImportsController : ControllerBase
{
    private readonly IVoterImportService _voterImportService;

    public VoterImportsController(
        IVoterImportService voterImportService)
    {
        _voterImportService = voterImportService;
    }

    [HttpPost]
    public async Task<IActionResult> Import(
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(
                "Please upload an Excel file.");
        }

        using var stream =
            file.OpenReadStream();

        var result =
            await _voterImportService
                .ImportAsync(stream);

        return Ok(result);
    }
}
