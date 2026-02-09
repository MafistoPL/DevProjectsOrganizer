namespace AppHost.Persistence;

public sealed class RootEntity
{
    public Guid Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = "not scanned";
    public DateTime CreatedAt { get; set; }
}
