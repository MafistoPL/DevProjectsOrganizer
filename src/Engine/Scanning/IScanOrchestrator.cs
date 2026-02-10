namespace Engine.Scanning;

public interface IScanOrchestrator
{
    IAsyncEnumerable<ScanEvent> RunAsync(ScanRequest request, CancellationToken cancellationToken);
}
