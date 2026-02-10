namespace Engine.Scanning;

public interface IScanHandle
{
    Guid ScanId { get; }
    IAsyncEnumerable<ScanEvent> Events { get; }
    Task PauseAsync(CancellationToken cancellationToken);
    Task ResumeAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
