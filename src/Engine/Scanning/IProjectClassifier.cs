namespace Engine.Scanning;

public interface IProjectClassifier
{
    Task<ProjectSuggestion> ClassifyAsync(
        string path,
        IReadOnlyList<string> markers,
        CancellationToken cancellationToken);
}
