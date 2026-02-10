namespace Engine.Scanning;

public interface IProjectDetector
{
    Task<IReadOnlyList<string>> DetectMarkersAsync(
        string path,
        CancellationToken cancellationToken);
}
