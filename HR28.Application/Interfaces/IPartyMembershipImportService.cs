using HR28.Application.DTOs.Imports;

namespace HR28.Application.Interfaces;

/// <summary>
/// Applies a party membership list (Excel) to the voter registry, matched by National ID:
/// sets the party, and fills date of birth, mobile number and gender where they are
/// empty (existing values are never overwritten). All or nothing; one audit entry.
/// </summary>
public interface IPartyMembershipImportService
{
    Task<MembershipImportResultDto> ImportAsync(Stream excelStream, string fileName, Guid partyId);
}
