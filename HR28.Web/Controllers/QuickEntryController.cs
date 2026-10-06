using HR28.Web.Filters;
using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

/// <summary>
/// Quick entry: find a voter by ID card, see their photo and details, and add an
/// encounter, a pledge or an influencer link on the same page (each form only with its
/// right). Every save reloads the whole page with the same voter. The API checks the
/// rights and the voter's area again on every call.
/// </summary>
[SessionAuthorize]
public class QuickEntryController : AppController
{
    private readonly ApiClient _apiClient;

    public QuickEntryController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    private bool Can(string right) => Hr28Permissions.Has(HttpContext.Session, right);

    private IActionResult NotAllowed()
    {
        TempData["FlashError"] = "Quick entry needs the rights to view voters and to add encounters, pledges or influencer links. Ask your Administrator.";
        return Hr28Permissions.StartsOnQuickEntry(HttpContext.Session)
            ? RedirectToAction("Index", "Voters")
            : RedirectToAction("Index", "Dashboard");
    }

    /// <summary>The page; with a voter id (after a search or a save) it shows that voter.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(Guid? voterId)
    {
        if (!Hr28Permissions.CanUseQuickEntry(HttpContext.Session))
            return NotAllowed();

        var model = new QuickEntryViewModel();

        if (voterId.HasValue)
        {
            var voter = await _apiClient.GetAsync<VoterSearchDto>($"Voters/{voterId}", Token);

            if (HandleApiFailure(voter) is { } redirect)
                return redirect;

            if (!voter.Success || voter.Data == null)
            {
                model.SearchMessage = "That voter could not be found in your areas.";
                return View(model);
            }

            model.Voter = voter.Data;
            model.NationalId = voter.Data.NationalId;
        }

        return await ShowAsync(model);
    }

    /// <summary>Find a voter by ID card. Posted (not in the address) so the ID isn't kept in browser history.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Find(string? nationalId)
    {
        if (!Hr28Permissions.CanUseQuickEntry(HttpContext.Session))
            return NotAllowed();

        var nid = (nationalId ?? string.Empty).Trim().ToUpperInvariant();
        var model = new QuickEntryViewModel { NationalId = nid };

        if (!System.Text.RegularExpressions.Regex.IsMatch(nid, @"^[A-Z]\d{6}$"))
        {
            model.SearchMessage = "Type the ID card number as one letter and 6 digits, e.g. A123456.";
            return View("Index", model);
        }

        var voter = await _apiClient.GetAsync<VoterSearchDto>($"Voters/by-national-id/{Uri.EscapeDataString(nid)}", Token);

        if (HandleApiFailure(voter) is { } redirect)
            return redirect;

        if (!voter.Success || voter.Data == null)
        {
            model.SearchMessage = voter.StatusCode == System.Net.HttpStatusCode.NotFound
                ? $"No voter with ID card {nid} was found {Hr28Permissions.SearchAreaText(HttpContext.Session)}. Check the number and try again."
                : string.IsNullOrWhiteSpace(voter.Message) ? "The voter could not be loaded. Please try again." : voter.Message;
            return View("Index", model);
        }

        // Show the voter on a normal page address, so a refresh doesn't resend the search.
        return RedirectToAction(nameof(Index), new { voterId = voter.Data.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEncounter([Bind(Prefix = "Encounter")] QuickEncounterForm form)
    {
        if (!Can(Hr28Permissions.EncountersAdd))
            return NotAllowed();

        if (!ModelState.IsValid)
            return await RedisplayAsync(form.VoterId, m => { m.Encounter = form; m.EncounterError = FirstError(); });

        var result = await _apiClient.PostAsync<object>("Encounters", new
        {
            form.VoterId,
            form.EncounterDate,
            form.EncounterType,
            form.Outcome,
            // Ignored by the API without "Set encounter response".
            Response = Can(Hr28Permissions.EncountersResponse) ? form.Response : null,
            Notes = form.Notes ?? string.Empty
        }, Token);

        if (result.IsUnauthorized)
            return HandleApiFailure(result)!;

        if (!result.Success)
            return await RedisplayAsync(form.VoterId, m => { m.Encounter = form; m.EncounterError = Message(result.Message, "The encounter could not be saved."); });

        TempData["SuccessMessage"] = "Encounter recorded.";
        return RedirectToAction(nameof(Index), new { voterId = form.VoterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPledge([Bind(Prefix = "Pledge")] QuickPledgeForm form)
    {
        if (!Can(Hr28Permissions.PledgesAdd))
            return NotAllowed();

        if (!ModelState.IsValid)
            return await RedisplayAsync(form.VoterId, m => { m.Pledge = form; m.PledgeError = FirstError(); });

        var result = await _apiClient.PostAsync<object>("Pledges", new
        {
            form.VoterId,
            form.Title,
            form.Description,
            form.PledgeDate,
            form.DueDate
        }, Token);

        if (result.IsUnauthorized)
            return HandleApiFailure(result)!;

        if (!result.Success)
            return await RedisplayAsync(form.VoterId, m => { m.Pledge = form; m.PledgeError = Message(result.Message, "The pledge could not be saved."); });

        TempData["SuccessMessage"] = "Pledge recorded.";
        return RedirectToAction(nameof(Index), new { voterId = form.VoterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkInfluencer([Bind(Prefix = "Link")] QuickLinkForm form)
    {
        if (!Can(Hr28Permissions.InfluencersLink))
            return NotAllowed();

        if (!ModelState.IsValid)
            return await RedisplayAsync(form.VoterId, m => { m.Link = form; m.LinkError = FirstError(); });

        var result = await _apiClient.PostAsync<object>("Influencers/link", new
        {
            form.VoterId,
            InfluencerId = form.InfluencerId!.Value,
            form.RelationshipType
        }, Token);

        if (result.IsUnauthorized)
            return HandleApiFailure(result)!;

        if (!result.Success)
            return await RedisplayAsync(form.VoterId, m => { m.Link = form; m.LinkError = Message(result.Message, "The influencer could not be linked."); });

        TempData["SuccessMessage"] = "Influencer linked.";
        return RedirectToAction(nameof(Index), new { voterId = form.VoterId });
    }

    /// <summary>Shows the page again after a failed save, keeping what was typed in that form.</summary>
    private async Task<IActionResult> RedisplayAsync(Guid voterId, Action<QuickEntryViewModel> keep)
    {
        var model = new QuickEntryViewModel();

        var voter = await _apiClient.GetAsync<VoterSearchDto>($"Voters/{voterId}", Token);

        if (HandleApiFailure(voter) is { } redirect)
            return redirect;

        if (voter.Success && voter.Data != null)
        {
            model.Voter = voter.Data;
            model.NationalId = voter.Data.NationalId;
        }
        else
        {
            model.SearchMessage = "That voter could not be found in your areas.";
        }

        keep(model);

        return await ShowAsync(model);
    }

    private async Task<IActionResult> ShowAsync(QuickEntryViewModel model)
    {
        if (model.Voter != null)
        {
            model.Encounter.VoterId = model.Voter.Id;
            model.Pledge.VoterId = model.Voter.Id;
            model.Link.VoterId = model.Voter.Id;

            // The influencer list for the link form (everyone; influencers are global).
            if (Can(Hr28Permissions.InfluencersLink))
            {
                var influencers = await _apiClient.GetAsync<List<InfluencerDto>>("Influencers", Token);
                model.Influencers = influencers.Data ?? new();
            }
        }

        return View("Index", model);
    }

    private string FirstError() =>
        ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
        ?? "Please check the form and try again.";

    private static string Message(string? apiMessage, string fallback) =>
        string.IsNullOrWhiteSpace(apiMessage) ? fallback : apiMessage;
}
