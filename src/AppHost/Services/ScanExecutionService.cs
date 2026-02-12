namespace AppHost.Services;

public sealed class ScanExecutionService
{
    private readonly ScanSnapshotBuilder _snapshotBuilder;
    private readonly ScanSnapshotWriter _snapshotWriter;

    public ScanExecutionService(
        ScanSnapshotBuilder? snapshotBuilder = null,
        ScanSnapshotWriter? snapshotWriter = null)
    {
        _snapshotBuilder = snapshotBuilder ?? new ScanSnapshotBuilder();
        _snapshotWriter = snapshotWriter ?? new ScanSnapshotWriter();
    }

    public async Task<string> ExecuteAsync(
        ScanRuntime runtime,
        Func<ScanRuntime, Task> reportStateAsync)
    {
        var shouldCount = runtime.Request.Mode != "whole";
        if (shouldCount)
        {
            runtime.SetState(ScanSessionStates.Counting);
            await reportStateAsync(runtime);
            runtime.TotalFiles = await _snapshotBuilder.CountFilesAsync(runtime);
        }

        runtime.SetState(ScanSessionStates.Running);
        await reportStateAsync(runtime);

        var snapshot = await _snapshotBuilder.BuildSnapshotAsync(runtime, reportStateAsync);
        return await _snapshotWriter.SaveAsync(snapshot, runtime.ScanId, runtime.StopToken);
    }
}
