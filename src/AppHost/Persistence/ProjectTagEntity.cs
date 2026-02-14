namespace AppHost.Persistence;

public sealed class ProjectTagEntity
{
    public Guid ProjectId { get; set; }
    public Guid TagId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
