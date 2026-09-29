namespace HR28.Application.DTOs.Reports;

public class PledgeStatusSummaryDto
{
    public int Pending { get; set; }

    public int InProgress { get; set; }

    public int Completed { get; set; }

    public int Cancelled { get; set; }
}