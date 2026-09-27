using HR28.Application.DTOs.Voters;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HR28.API.Controllers;

[Authorize]
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
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
        {
            return Unauthorized();
        }

        var userId =
            Guid.Parse(userIdClaim.Value);

        var result =
            await _voterService.GetVotersAsync(userId);

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
    //[Authorize]
    [HttpGet("search")]
    public async Task<IActionResult> SearchVoters(
    string searchTerm)
    {
        var result =
            await _voterService.SearchVotersAsync(searchTerm);

        return Ok(result);
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateVoter(
    Guid id,
    UpdateVoterDto request)
    {
        await _voterService.UpdateVoterAsync(
            id,
            request);

        return Ok("Voter updated successfully.");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteVoter(Guid id)
    {
        await _voterService.DeleteVoterAsync(id);

        return Ok("Voter deleted successfully.");
    }

}