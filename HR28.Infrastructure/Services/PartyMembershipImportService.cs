using System.Data;
using System.Globalization;
using ExcelDataReader;
using HR28.Application.Common;
using HR28.Application.DTOs.Imports;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace HR28.Infrastructure.Services;

/// <summary>
/// Party membership list upload (Voters → Upload membership list). The file is read
/// into a temporary table and applied with fixed, parameterised statements in one
/// transaction, so 50,000 rows take seconds and either all changes apply or none do.
/// Existing values are never overwritten, except the party itself.
/// </summary>
public class PartyMembershipImportService : IPartyMembershipImportService
{
    private const int MaxListed = 100;

    private readonly HR28DbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PartyMembershipImportService> _logger;

    public PartyMembershipImportService(
        HR28DbContext dbContext,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PartyMembershipImportService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(
            _httpContextAccessor.HttpContext?.User
                .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var id)
            ? id
            : null;

    private sealed record Member(string NationalId, string? Gender, DateOnly? DateOfBirth, string? Mobile);

    public async Task<MembershipImportResultDto> ImportAsync(Stream excelStream, string fileName, Guid partyId)
    {
        var party = await _dbContext.PoliticalParties
            .Where(p => p.Id == partyId)
            .Select(p => new { p.Name, p.ShortName })
            .FirstOrDefaultAsync()
            ?? throw new BusinessRuleException("Please choose a party from the list.");

        var result = new MembershipImportResultDto { PartyName = party.Name };

        List<Member> members;

        try
        {
            members = ReadMembers(excelStream, result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not BusinessRuleException)
        {
            _logger.LogWarning(ex, "Membership list could not be read");
            throw new BusinessRuleException("The file could not be read as an Excel workbook. Check the file and try again.");
        }

        if (members.Count > 0)
        {
            try
            {
                await ApplyAsync(members, partyId, result);
            }
            catch (Exception ex) when (ex is SqlException or DbUpdateException or InvalidOperationException)
            {
                _logger.LogError(ex, "Membership list could not be applied");
                result.ErrorMessage = "The list could not be applied, so nothing was changed. Please try again.";
            }
        }

        var name = Path.GetFileName(fileName ?? string.Empty);

        await _auditService.LogAsync(
            CurrentUserId,
            $"Membership list ({party.ShortName}) from \"{(name.Length > 80 ? name[..80] : name)}\": " +
            (result.ErrorMessage != null
                ? "not applied (error)"
                : $"{result.Matched:N0} matched, {result.PartySet:N0} set to {party.ShortName} " +
                  $"({result.MovedFromOtherParty:N0} from another party), {result.AlreadyMembers:N0} already, " +
                  $"{result.DateOfBirthFilled:N0} dates of birth, {result.MobileFilled:N0} mobiles, " +
                  $"{result.GenderFilled:N0} genders filled, {result.GenderConflictCount:N0} gender conflicts kept, " +
                  $"{result.NotInRegistryCount:N0} not in registry, {result.WithoutNationalId:N0} without National ID"),
            "Voter",
            null);

        return result;
    }

    // ---------- Reading the file ----------

    private static List<Member> ReadMembers(Stream stream, MembershipImportResultDto result)
    {
        using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration { LeaveOpen = true });

        if (!reader.Read())
            throw new BusinessRuleException("The file is empty.");

        // Columns are found by their heading, so the order doesn't matter.
        var headings = Enumerable.Range(0, reader.FieldCount)
            .Select(i => (reader.GetValue(i)?.ToString() ?? string.Empty).Trim().ToUpperInvariant())
            .ToList();

        int Find(params string[] names) => headings.FindIndex(h => names.Contains(h));

        var nidColumn = Find("NID", "NATIONAL ID", "NATIONALID", "ID CARD", "ID");
        var genderColumn = Find("GENDER", "SEX");
        var dobColumn = Find("DOB", "DATE OF BIRTH", "BIRTH DATE", "DATEOFBIRTH");
        var phoneColumn = Find("PHONE", "MOBILE", "MOBILE NUMBER", "CONTACT", "CONTACT NUMBER");

        if (nidColumn < 0)
            throw new BusinessRuleException("The file needs a column headed \"NID\" (National ID) in the first row.");

        var members = new Dictionary<string, Member>(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (Enumerable.Range(0, reader.FieldCount).All(i => reader.GetValue(i) == null))
                continue; // blank row

            result.TotalRows++;

            var nid = MaldivesFormats.CleanNationalId(Text(reader, nidColumn));

            if (nid.Length == 0)
            {
                result.WithoutNationalId++;
                continue;
            }

            if (!MaldivesFormats.IsNationalId(nid))
            {
                result.InvalidNationalId++;
                continue;
            }

            if (members.ContainsKey(nid))
            {
                result.DuplicateRows++;
                continue;
            }

            members[nid] = new Member(
                nid,
                genderColumn < 0 ? null : Gender(Text(reader, genderColumn)),
                dobColumn < 0 ? null : DateOfBirth(reader.GetValue(dobColumn)),
                phoneColumn < 0 ? null : Mobile(Text(reader, phoneColumn)));
        }

        return members.Values.ToList();
    }

    private static string Text(IExcelDataReader reader, int column) =>
        column < reader.FieldCount ? (reader.GetValue(column)?.ToString() ?? string.Empty).Trim() : string.Empty;

    /// <summary>Stored as the registry does: "M" or "F".</summary>
    private static string? Gender(string value) =>
        value.ToUpperInvariant() switch
        {
            "M" or "MALE" => "M",
            "F" or "FEMALE" => "F",
            _ => null
        };

    private static readonly string[] DateFormats =
        { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "dd MMM yyyy" };

    private static DateOnly? DateOfBirth(object? value)
    {
        DateOnly? date = value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            double serial when serial is > 1 and < 80000 => DateOnly.FromDateTime(DateTime.FromOADate(serial)),
            string text when DateTime.TryParseExact(text.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                => DateOnly.FromDateTime(parsed),
            _ => null
        };

        // Plausible dates only (born 1900 or later, not in the future).
        return date is { } d && d.Year >= 1900 && d <= DateOnly.FromDateTime(MaldivesTime.Now) ? d : null;
    }

    /// <summary>A 7-digit Maldives number (a leading 960 country code is removed), or null.</summary>
    private static string? Mobile(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.Length == 10 && digits.StartsWith("960", StringComparison.Ordinal))
            digits = digits[3..];

        return MaldivesFormats.IsMobile(digits) ? digits : null;
    }

    // ---------- Applying it ----------

    private async Task ApplyAsync(List<Member> members, Guid partyId, MembershipImportResultDto result)
    {
        var connection = (SqlConnection)_dbContext.Database.GetDbConnection();

        await _dbContext.Database.OpenConnectionAsync();

        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();

            await Execute(connection, sqlTransaction,
                "CREATE TABLE #Members (NationalId nvarchar(20) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY, " +
                "Gender nvarchar(1) NULL, DateOfBirth date NULL, Mobile nvarchar(7) NULL);");

            var table = new DataTable();
            table.Columns.Add("NationalId", typeof(string));
            table.Columns.Add("Gender", typeof(string));
            table.Columns.Add("DateOfBirth", typeof(DateTime));
            table.Columns.Add("Mobile", typeof(string));

            foreach (var m in members)
            {
                table.Rows.Add(
                    m.NationalId,
                    (object?)m.Gender ?? DBNull.Value,
                    m.DateOfBirth is { } d ? d.ToDateTime(TimeOnly.MinValue) : DBNull.Value,
                    (object?)m.Mobile ?? DBNull.Value);
            }

            using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, sqlTransaction)
            {
                DestinationTableName = "#Members",
                BulkCopyTimeout = 120
            })
            {
                await bulk.WriteToServerAsync(table);
            }

            const string Joined = "FROM Voters v INNER JOIN #Members m ON m.NationalId = v.NationalId";

            result.Matched = await Scalar(connection, sqlTransaction, $"SELECT COUNT(*) {Joined};", partyId);

            result.AlreadyMembers = await Scalar(connection, sqlTransaction,
                $"SELECT COUNT(*) {Joined} WHERE v.PoliticalPartyId = @party;", partyId);

            result.MovedFromOtherParty = await Scalar(connection, sqlTransaction,
                $"SELECT COUNT(*) {Joined} WHERE v.PoliticalPartyId IS NOT NULL AND v.PoliticalPartyId <> @party;", partyId);

            result.GenderConflictCount = await Scalar(connection, sqlTransaction,
                $"SELECT COUNT(*) {Joined} WHERE m.Gender IS NOT NULL AND v.Gender <> '' AND v.Gender <> m.Gender;", partyId);

            result.GenderConflicts = await List(connection, sqlTransaction,
                $"SELECT TOP ({MaxListed}) m.NationalId {Joined} " +
                "WHERE m.Gender IS NOT NULL AND v.Gender <> '' AND v.Gender <> m.Gender ORDER BY m.NationalId;");

            result.NotInRegistryCount = await Scalar(connection, sqlTransaction,
                "SELECT COUNT(*) FROM #Members m WHERE NOT EXISTS (SELECT 1 FROM Voters v WHERE v.NationalId = m.NationalId);", partyId);

            result.NotInRegistry = await List(connection, sqlTransaction,
                $"SELECT TOP ({MaxListed}) m.NationalId FROM #Members m " +
                "WHERE NOT EXISTS (SELECT 1 FROM Voters v WHERE v.NationalId = m.NationalId) ORDER BY m.NationalId;");

            // Only empty fields are filled; the registry's values always win.
            result.DateOfBirthFilled = await Execute(connection, sqlTransaction,
                $"UPDATE v SET v.DateOfBirth = m.DateOfBirth {Joined} WHERE v.DateOfBirth IS NULL AND m.DateOfBirth IS NOT NULL;");

            result.MobileFilled = await Execute(connection, sqlTransaction,
                $"UPDATE v SET v.MobileNumber = m.Mobile {Joined} WHERE ISNULL(v.MobileNumber, '') = '' AND m.Mobile IS NOT NULL;");

            result.GenderFilled = await Execute(connection, sqlTransaction,
                $"UPDATE v SET v.Gender = m.Gender {Joined} WHERE ISNULL(v.Gender, '') = '' AND m.Gender IS NOT NULL;");

            result.PartySet = await Execute(connection, sqlTransaction,
                $"UPDATE v SET v.PoliticalPartyId = @party {Joined} " +
                "WHERE v.PoliticalPartyId IS NULL OR v.PoliticalPartyId <> @party;", partyId);

            await Execute(connection, sqlTransaction, "DROP TABLE #Members;");

            await transaction.CommitAsync();
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }

    private static SqlCommand Command(SqlConnection connection, SqlTransaction transaction, string sql, Guid? partyId)
    {
        var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 120 };

        if (partyId.HasValue)
            command.Parameters.Add(new SqlParameter("@party", SqlDbType.UniqueIdentifier) { Value = partyId.Value });

        return command;
    }

    private static async Task<int> Execute(SqlConnection connection, SqlTransaction transaction, string sql, Guid? partyId = null)
    {
        await using var command = Command(connection, transaction, sql, partyId);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> Scalar(SqlConnection connection, SqlTransaction transaction, string sql, Guid partyId)
    {
        await using var command = Command(connection, transaction, sql, partyId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<List<string>> List(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        await using var command = Command(connection, transaction, sql, null);
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();

        while (await reader.ReadAsync())
            values.Add(reader.GetString(0));

        return values;
    }
}
