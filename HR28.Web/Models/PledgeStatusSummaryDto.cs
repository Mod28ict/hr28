namespace HR28.Web.Models;

public class PledgeStatusSummaryDto
{
    public int Pending { get; set; }

    public int InProgress { get; set; }

    public int Completed { get; set; }

    public int Cancelled { get; set; }
}