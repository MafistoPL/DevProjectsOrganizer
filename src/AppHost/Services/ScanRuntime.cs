using System.IO;
using System.Linq;

namespace AppHost.Services;

public sealed class ScanRuntime
{
    private readonly AsyncManualResetEvent _pauseEvent = new(true);
    private DateTimeOffset _lastProgressAt = DateTimeOffset.MinValue;
    private readonly CancellationTokenSource _cts = new();

    public Guid ScanId { get; }
    public string RootPath { get; }
    public string DiskKey { get; }
    public ScanStartRequest Request { get; }
    public string State { get; private set; } = "Queued";
    public long FilesScanned { get; set; }
    public long? TotalFiles { get; set; }
    public string? CurrentPath { get; set; }
    public string? OutputPath { get; set; }
    public string? QueueReason { get; set; }
    public bool HoldsWholeLock { get; set; }

    public ScanRuntime(Guid scanId, string rootPath, string diskKey, ScanStartRequest request)
    {
        ScanId = scanId;
        RootPath = rootPath;
        DiskKey = diskKey;
        Request = request;
    }

    public CancellationToken StopToken => _cts.Token;

    public void Pause()
    {
        if (State == "Paused")
        {
            return;
        }

        _pauseEvent.Reset();
        State = "Paused";
    }

    public void Resume()
    {
        if (State != "Paused")
        {
            return;
        }

        _pauseEvent.Set();
        State = "Running";
    }

    public void Stop()
    {
        _cts.Cancel();
    }

    public void SetState(string state)
    {
        State = state;
    }

    public async Task WaitIfPausedAsync()
    {
        await _pauseEvent.WaitAsync(_cts.Token);
    }

    public bool HasDepthRemaining(int depth)
    {
        if (Request.DepthLimit is null)
        {
            return true;
        }

        return depth < Request.DepthLimit.Value;
    }

    public IEnumerable<string> GetRootPaths()
    {
        if (Request.Mode == "whole")
        {
            return DriveInfo.GetDrives()
                .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
                .Select(drive => drive.RootDirectory.FullName);
        }

        return new[] { RootPath };
    }

    public bool ShouldReportProgress()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastProgressAt < TimeSpan.FromMilliseconds(500))
        {
            return false;
        }

        _lastProgressAt = now;
        return true;
    }

    public ScanSessionDto ToDto()
    {
        return new ScanSessionDto(
            ScanId,
            RootPath,
            Request.Mode,
            State,
            DiskKey,
            CurrentPath,
            FilesScanned,
            TotalFiles,
            QueueReason,
            OutputPath);
    }
}
