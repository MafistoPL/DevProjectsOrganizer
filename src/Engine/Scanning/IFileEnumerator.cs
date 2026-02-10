namespace Engine.Scanning;

public interface IFileEnumerator
{
    IAsyncEnumerable<string> EnumerateAsync(
        ScanRootTarget root,
        ScanOptions options,
        CancellationToken cancellationToken);
}
