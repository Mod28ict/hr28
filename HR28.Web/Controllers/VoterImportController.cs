using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

/// <summary>
/// Upload the voter list spreadsheet. Administrators only (Administrator and
/// National Administrator); the API enforces the same rule and audits every upload.
/// </summary>
public class VoterImportController : AppController
{
    private const long MaxFileBytes = 20 * 1024 * 1024;

    private readonly ApiClient _apiClient;

    public VoterImportController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!IsAdministrator)
            return NotAllowed();

        return View(new VoterImportViewModel());
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> Index(IFormFile? file)
    {
        if (!IsAdministrator)
            return NotAllowed();

        var model = new VoterImportViewModel { FileName = file?.FileName };

        var extension = Path.GetExtension(file?.FileName ?? string.Empty).ToLowerInvariant();

        if (file == null || file.Length == 0)
            model.ErrorMessage = "Choose an Excel file to upload.";
        else if (file.Length > MaxFileBytes)
            model.ErrorMessage = "The file is larger than 20 MB. Split it into smaller files and upload them one at a time.";
        else if (extension != ".xlsx" && extension != ".xls")
            model.ErrorMessage = "Only Excel files (.xlsx or .xls) can be uploaded.";

        if (model.ErrorMessage != null)
            return View(model);

        var result = await _apiClient.PostFileAsync<VoterImportResult>("VoterImports", file!, Token);

        if (!result.Success)
        {
            var redirect = HandleApiFailure(result);

            if (redirect != null)
                return redirect;

            model.ErrorMessage = result.Message;
            return View(model);
        }

        model.Result = result.Data ?? new VoterImportResult();

        return View(model);
    }

    /// <summary>Party membership list: sets the party and fills empty date of birth, mobile and gender.</summary>
    [HttpGet]
    public async Task<IActionResult> Membership()
    {
        if (!IsAdministrator)
            return NotAllowed();

        var model = new MembershipImportViewModel();

        if (await LoadPartiesAsync(model) is { } failure)
            return failure;

        model.PartyId = model.Parties.FirstOrDefault(p => p.IsDefaultFilter)?.Id;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> Membership(Guid? partyId, IFormFile? file)
    {
        if (!IsAdministrator)
            return NotAllowed();

        var model = new MembershipImportViewModel { PartyId = partyId, FileName = file?.FileName };

        if (await LoadPartiesAsync(model) is { } failure)
            return failure;

        var extension = Path.GetExtension(file?.FileName ?? string.Empty).ToLowerInvariant();

        if (partyId == null || model.Parties.All(p => p.Id != partyId))
            model.ErrorMessage = "Please choose the party the list belongs to.";
        else if (file == null || file.Length == 0)
            model.ErrorMessage = "Choose an Excel file to upload.";
        else if (file.Length > MaxFileBytes)
            model.ErrorMessage = "The file is larger than 20 MB. Split it into smaller files and upload them one at a time.";
        else if (extension != ".xlsx" && extension != ".xls")
            model.ErrorMessage = "Only Excel files (.xlsx or .xls) can be uploaded.";

        if (model.ErrorMessage != null)
            return View(model);

        var result = await _apiClient.PostFileAsync<MembershipImportResult>(
            $"MembershipImports?partyId={partyId}", file!, Token);

        if (!result.Success)
        {
            if (HandleApiFailure(result) is { } redirect)
                return redirect;

            model.ErrorMessage = result.Message;
            return View(model);
        }

        model.Result = result.Data ?? new MembershipImportResult();

        return View(model);
    }

    private async Task<IActionResult?> LoadPartiesAsync(MembershipImportViewModel model)
    {
        var parties = await _apiClient.GetAsync<List<PoliticalPartyDto>>("PoliticalParties", Token);

        if (HandleApiFailure(parties) is { } redirect)
            return redirect;

        model.Parties = parties.Data ?? new();

        return null;
    }

    private IActionResult NotAllowed()
    {
        TempData["FlashError"] = "Only administrators can upload voter lists.";
        return RedirectToAction("Index", "Dashboard");
    }
}
