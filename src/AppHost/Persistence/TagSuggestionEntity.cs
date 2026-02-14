namespace AppHost.Persistence;

public sealed class TagSuggestionEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TagId { get; set; }
    public string SuggestedTagName { get; set; } = string.Empty;
    public TagSuggestionType Type { get; set; } = TagSuggestionType.AssignExisting;
    public TagSuggestionSource Source { get; set; } = TagSuggestionSource.Heuristic;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public TagSuggestionStatus Status { get; set; } = TagSuggestionStatus.Pending;
}
