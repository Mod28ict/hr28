namespace HR28.Web.Models;

public class RecentActivityDto
{
    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public DateTime CreatedAt { get; set; }
}