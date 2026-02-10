namespace Engine.Scanning;

public interface IScanResultSink
{
    Task HandleAsync(ScanEvent scanEvent, CancellationToken cancellationToken);
}
