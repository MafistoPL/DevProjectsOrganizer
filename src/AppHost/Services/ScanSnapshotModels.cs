using System.Text.Json.Serialization;

namespace AppHost.Services;

public sealed class ScanSnapshot
{
    public Guid ScanId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public int? DepthLimit { get; set; }
    public long FilesScanned { get; set; }
    public long? TotalFiles { get; set; }
    public List<DirectoryNode> Roots { get; set; } = new();
}

public sealed class DirectoryNode
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public List<DirectoryNode> Directories { get; set; } = new();
    public List<FileNode> Files { get; set; } = new();
}

public sealed class FileNode
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public long Size { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? SampleLines { get; set; }

    public bool SampleTruncated { get; set; }
}
