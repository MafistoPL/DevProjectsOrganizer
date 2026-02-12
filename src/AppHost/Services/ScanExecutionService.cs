namespace AppHost.Services;

public sealed record ScanExecutionResult(
    string OutputPath,
    IReadOnlyList<DetectedProjectSuggestion> Suggestions
);

public sealed class ScanExecutionService
{
    private readonly ScanSnapshotBuilder _snapshotBuilder;
    private readonly ScanSnapshotWriter _snapshotWriter;
    private readonly ProjectSuggestionHeuristicsService _heuristicsService;

    public ScanExecutionService(
        ScanSnapshotBuilder? snapshotBuilder = null,
        ScanSnapshotWriter? snapshotWriter = null,
        ProjectSuggestionHeuristicsService? heuristicsService = null)
    {
        _snapshotBuilder = snapshotBuilder ?? new ScanSnapshotBuilder();
        _snapshotWriter = snapshotWriter ?? new ScanSnapshotWriter();
        _heuristicsService = heuristicsService ?? new ProjectSuggestionHeuristicsService();
    }

    public async Task<ScanExecutionResult> ExecuteAsync(
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
        var outputPath = await _snapshotWriter.SaveAsync(snapshot, runtime.ScanId, runtime.StopToken);
        var suggestions = _heuristicsService.Detect(snapshot);
        return new ScanExecutionResult(outputPath, suggestions);
    }
}
