namespace Engine.Scanning;

public interface IScanScheduler
{
    Task<IScanHandle> EnqueueAsync(ScanRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScanProgress>> GetActiveScansAsync(CancellationToken cancellationToken);
}
