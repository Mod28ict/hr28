using HR28.Application.DTOs.Imports;

namespace HR28.Application.Interfaces;

public interface IVoterImportService
{
    Task<ImportResultDto> ImportAsync(
        Stream excelStream,
        string fileName);
}