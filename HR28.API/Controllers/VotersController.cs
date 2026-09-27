using HR28.Application.DTOs.Voters;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VotersController : ControllerBase
{
    private readonly IVoterService _voterService;

    public VotersController(IVoterService voterService)
    {
        _voterService = voterService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateVoter(
        CreateVoterDto request)
    {
        var result =
            await _voterService.CreateVoterAsync(request);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetVoters()
    {
        var result =
            await _voterService.GetVotersAsync();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetVoter(Guid id)
    {
        var result =
            await _voterService.GetVoterByIdAsync(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }
    [HttpGet("search")]
    public async Task<IActionResult> SearchVoters(
    string searchTerm)
    {
        var result =
            await _voterService.SearchVotersAsync(searchTerm);

        return Ok(result);
    }
}