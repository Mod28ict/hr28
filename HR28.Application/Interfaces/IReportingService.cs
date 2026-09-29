using HR28.Application.DTOs.Reports;

namespace HR28.Application.Interfaces;

public interface IReportingService
{
    Task<List<ConstituencySummaryDto>>
        GetConstituencySummaryAsync();
}
