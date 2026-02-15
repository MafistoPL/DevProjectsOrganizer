namespace AppHost.Services;

public sealed class TagHeuristicsScanSnapshot
{
    public Guid RunId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public int AvailableTags { get; set; }
    public int DetectedSuggestions { get; set; }
    public int GeneratedSuggestions { get; set; }
    public List<TagHeuristicsScanSuggestion> Suggestions { get; set; } = new();
}

public sealed class TagHeuristicsScanSuggestion
{
    public Guid? TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
