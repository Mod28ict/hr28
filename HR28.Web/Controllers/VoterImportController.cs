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

    private IActionResult NotAllowed()
    {
        TempData["FlashError"] = "Only administrators can upload voter lists.";
        return RedirectToAction("Index", "Dashboard");
    }
}
