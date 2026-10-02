using ExcelDataReader;
using HR28.Application.DTOs.Imports;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HR28.Infrastructure.Services;

public class VoterImportService : IVoterImportService
{
    private const int BatchSize = 5000;
    private const int MaximumReturnedErrors = 100;

    private readonly HR28DbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<VoterImportService> _logger;

    public VoterImportService(
        HR28DbContext dbContext,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<VoterImportService> logger)
    {
        _logger = logger;
        _dbContext = dbContext;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor
            .HttpContext?
            .User
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdClaim?.Value, out var id) ? id : null;
    }

    public async Task<ImportResultDto> ImportAsync(
        Stream excelStream,
        string fileName)
    {
        ImportResultDto result;

        try
        {
            result = await ImportRowsAsync(excelStream);
        }
        catch (DbUpdateException ex)
        {
            // Rows saved in earlier batches stay saved; the audit entry records the stop.
            _logger.LogError(ex, "Voter import stopped while saving");

            result = new ImportResultDto { Errors = 1, Stopped = true };
            result.ErrorMessages.Add(
                "The import stopped while saving. Rows saved before that point are kept. Please try again.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not a readable workbook (wrong type, damaged or password-protected).
            _logger.LogWarning(ex, "Voter import file could not be read");

            result = new ImportResultDto { Errors = 1, Stopped = true };
            result.ErrorMessages.Add(
                "The file could not be read as an Excel workbook. Check it is the voter list template and try again.");
        }

        // One audit entry per upload, with the outcome, so bulk changes are traceable.
        var name = Path.GetFileName(fileName ?? string.Empty);

        await _auditService.LogAsync(
            GetCurrentUserId(),
            $"Import voters from \"{(name.Length > 80 ? name[..80] : name)}\": " +
            $"{result.VotersInserted} added, {result.VotersUpdated} updated, " +
            $"{result.VotersUnchanged} unchanged, {result.ConstituenciesCreated} constituencies created, " +
            $"{result.Errors} rows with errors" +
            (result.Stopped ? " (import stopped early)" : string.Empty),
            "Voter",
            null);

        return result;
    }

    private async Task<ImportResultDto> ImportRowsAsync(
        Stream excelStream)
    {
        var result = new ImportResultDto();

        if (excelStream == null || !excelStream.CanRead)
        {
            result.Errors++;
            result.ErrorMessages.Add(
                "The uploaded Excel stream could not be read.");

            return result;
        }

        ExcelReaderConfiguration readerConfiguration = new()
        {
            LeaveOpen = true
        };

        using var reader = ExcelReaderFactory.CreateReader(
            excelStream,
            readerConfiguration);

        var constituencyLookup =
            await LoadExistingConstituenciesAsync();

        var pendingRows = new List<ImportVoterRow>(
            BatchSize);

        var rowNumber = 0;

        do
        {
            while (reader.Read())
            {
                rowNumber++;

                // Skip the header row on the first worksheet.
                if (rowNumber == 1)
                {
                    continue;
                }

                result.TotalRows++;

                try
                {
                    var row = ReadRow(
                        reader,
                        rowNumber);

                    if (row == null)
                    {
                        result.Errors++;

                        AddError(
                            result,
                            $"Row {rowNumber}: missing National ID.");

                        continue;
                    }

                    if (row.Gender != "M" &&
                        row.Gender != "F")
                    {
                        result.Errors++;

                        AddError(
                            result,
                            $"Row {rowNumber}: invalid gender " +
                            $"'{row.Gender}' for {row.NationalId}.");

                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(
                        row.ConstituencyCode))
                    {
                        result.Errors++;

                        AddError(
                            result,
                            $"Row {rowNumber}: missing constituency " +
                            $"code for {row.NationalId}.");

                        continue;
                    }

                    pendingRows.Add(row);

                    if (pendingRows.Count >= BatchSize)
                    {
                        await ProcessBatchAsync(
                            pendingRows,
                            constituencyLookup,
                            result);

                        pendingRows.Clear();

                        _dbContext.ChangeTracker.Clear();
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or FormatException or InvalidCastException)
                {
                    result.Errors++;

                    AddError(
                        result,
                        ex is InvalidOperationException
                            ? $"Row {rowNumber}: {ex.Message}"
                            : $"Row {rowNumber}: could not be read.");
                }
            }

            /*
             * The supplied workbook has one worksheet.
             * Stop here so another worksheet with a different
             * structure is not imported accidentally.
             */
            break;
        }
        while (reader.NextResult());

        if (pendingRows.Count > 0)
        {
            await ProcessBatchAsync(
                pendingRows,
                constituencyLookup,
                result);

            _dbContext.ChangeTracker.Clear();
        }

        return result;
    }

    private async Task<Dictionary<string, Guid>>
        LoadExistingConstituenciesAsync()
    {
        var constituencies = await _dbContext.Constituencies
            .AsNoTracking()
            .Where(x =>
                x.Code != null &&
                x.Code != string.Empty)
            .Select(x => new
            {
                x.Id,
                x.Code
            })
            .ToListAsync();

        return constituencies
            .GroupBy(x => x.Code!.Trim())
            .ToDictionary(
                group => group.Key,
                group => group.First().Id,
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task ProcessBatchAsync(
        List<ImportVoterRow> rows,
        Dictionary<string, Guid> constituencyLookup,
        ImportResultDto result)
    {
        await EnsureConstituenciesExistAsync(
            rows,
            constituencyLookup,
            result);

        var nationalIds = rows
            .Select(x => x.NationalId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingVoters =
            await _dbContext.Voters
                .Where(x =>
                    nationalIds.Contains(x.NationalId))
                .ToDictionaryAsync(
                    x => x.NationalId,
                    StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!constituencyLookup.TryGetValue(
                row.ConstituencyCode,
                out var constituencyId))
            {
                result.Errors++;

                AddError(
                    result,
                    $"No constituency mapping for " +
                    $"{row.ConstituencyCode}, voter " +
                    $"{row.NationalId}.");

                continue;
            }

            if (existingVoters.TryGetValue(
                row.NationalId,
                out var voter))
            {
                if (UpdateVoterIfChanged(
                    voter,
                    row,
                    constituencyId))
                {
                    result.VotersUpdated++;
                }
                else
                {
                    result.VotersUnchanged++;
                }

                continue;
            }

            var newVoter = new Voter
            {
                Id = Guid.NewGuid(),
                NationalId = row.NationalId,
                FullName = row.FullName,
                Gender = row.Gender,
                Address = row.Address,
                Ward = row.Ward,
                RegisteredIsland = row.RegisteredIsland,
                AtollCode = row.AtollCode,
                ConstituencyCode = row.ConstituencyCode,
                ConstituencyName = row.ConstituencyName,
                ConstituencyId = constituencyId,

                /*
                 * The spreadsheet supplies registered-island text,
                 * not the HR28 Island table's internal GUID.
                 */
                IslandId = null,

                MobileNumber = string.Empty,
                Remarks = string.Empty,
                SupportStatus = "Undecided",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Voters.Add(newVoter);

            existingVoters[row.NationalId] =
                newVoter;

            result.VotersInserted++;
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task EnsureConstituenciesExistAsync(
        List<ImportVoterRow> rows,
        Dictionary<string, Guid> constituencyLookup,
        ImportResultDto result)
    {
        var requiredConstituencies = rows
            .GroupBy(
                x => x.ConstituencyCode,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(x =>
                !constituencyLookup.ContainsKey(
                    x.ConstituencyCode))
            .ToList();

        if (requiredConstituencies.Count == 0)
        {
            return;
        }

        foreach (var row in requiredConstituencies)
        {
            var constituency = new Constituency
            {
                Id = Guid.NewGuid(),
                Code = row.ConstituencyCode,
                Name = row.ConstituencyName
            };

            _dbContext.Constituencies.Add(
                constituency);

            constituencyLookup[row.ConstituencyCode] =
                constituency.Id;

            result.ConstituenciesCreated++;
        }

        await _dbContext.SaveChangesAsync();
    }

    private static bool UpdateVoterIfChanged(
        Voter voter,
        ImportVoterRow row,
        Guid constituencyId)
    {
        var changed = false;

        changed |= SetIfDifferent(
            voter.FullName,
            row.FullName,
            value => voter.FullName = value);

        changed |= SetIfDifferent(
            voter.Gender,
            row.Gender,
            value => voter.Gender = value);

        changed |= SetIfDifferent(
            voter.Address,
            row.Address,
            value => voter.Address = value);

        changed |= SetIfDifferent(
            voter.Ward,
            row.Ward,
            value => voter.Ward = value);

        changed |= SetIfDifferent(
            voter.RegisteredIsland,
            row.RegisteredIsland,
            value =>
                voter.RegisteredIsland = value);

        changed |= SetIfDifferent(
            voter.AtollCode,
            row.AtollCode,
            value => voter.AtollCode = value);

        changed |= SetIfDifferent(
            voter.ConstituencyCode,
            row.ConstituencyCode,
            value =>
                voter.ConstituencyCode = value);

        changed |= SetIfDifferent(
            voter.ConstituencyName,
            row.ConstituencyName,
            value =>
                voter.ConstituencyName = value);

        if (voter.ConstituencyId != constituencyId)
        {
            voter.ConstituencyId = constituencyId;
            changed = true;
        }

        return changed;
    }

    private static bool SetIfDifferent(
        string? existingValue,
        string? incomingValue,
        Action<string> assign)
    {
        var existing = existingValue ?? string.Empty;
        var incoming = incomingValue ?? string.Empty;

        if (string.Equals(
            existing,
            incoming,
            StringComparison.Ordinal))
        {
            return false;
        }

        assign(incoming);

        return true;
    }

    private static ImportVoterRow? ReadRow(
        IExcelDataReader reader,
        int rowNumber)
    {
        if (reader.FieldCount < 9)
        {
            throw new InvalidOperationException(
                $"has {reader.FieldCount} columns; " +
                "the voter list template needs 9.");
        }

        var nationalId = GetText(reader, 0);

        if (string.IsNullOrWhiteSpace(nationalId))
        {
            return null;
        }

        return new ImportVoterRow
        {
            RowNumber = rowNumber,
            NationalId = nationalId,
            FullName = GetText(reader, 1),
            Gender = GetText(reader, 2)
                .ToUpperInvariant(),
            Address = GetText(reader, 3),
            Ward = GetNullableText(reader, 4),
            RegisteredIsland = GetText(reader, 5),
            AtollCode = GetText(reader, 6),
            ConstituencyCode = GetText(reader, 7),
            ConstituencyName = GetText(reader, 8)
        };
    }

    private static string GetText(
        IExcelDataReader reader,
        int index)
    {
        return reader.GetValue(index)?
            .ToString()?
            .Trim() ?? string.Empty;
    }

    private static string? GetNullableText(
        IExcelDataReader reader,
        int index)
    {
        var value = GetText(reader, index);

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private static void AddError(
        ImportResultDto result,
        string message)
    {
        if (result.ErrorMessages.Count <
            MaximumReturnedErrors)
        {
            result.ErrorMessages.Add(message);
        }
    }

    private sealed class ImportVoterRow
    {
        public int RowNumber { get; init; }

        public string NationalId { get; init; }
            = string.Empty;

        public string FullName { get; init; }
            = string.Empty;

        public string Gender { get; init; }
            = string.Empty;

        public string Address { get; init; }
            = string.Empty;

        public string? Ward { get; init; }

        public string RegisteredIsland { get; init; }
            = string.Empty;

        public string AtollCode { get; init; }
            = string.Empty;

        public string ConstituencyCode { get; init; }
            = string.Empty;

        public string ConstituencyName { get; init; }
            = string.Empty;
    }
}