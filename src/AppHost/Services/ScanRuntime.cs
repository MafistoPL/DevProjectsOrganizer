using System.IO;
using System.Linq;

namespace AppHost.Services;

public sealed class ScanRuntime
{
    private readonly AsyncManualResetEvent _pauseEvent = new(true);
    private DateTimeOffset _lastProgressAt = DateTimeOffset.MinValue;
    private DateTimeOffset? _scanStartedAt;
    private readonly CancellationTokenSource _cts = new();

    public Guid ScanId { get; }
    public string RootPath { get; }
    public string DiskKey { get; }
    public ScanStartRequest Request { get; }
    public string State { get; private set; } = ScanSessionStates.Queued;
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
        if (State == ScanSessionStates.Paused)
        {
            return;
        }

        _pauseEvent.Reset();
        State = ScanSessionStates.Paused;
    }

    public void Resume()
    {
        if (State != ScanSessionStates.Paused)
        {
            return;
        }

        _pauseEvent.Set();
        State = ScanSessionStates.Running;
    }

    public void Stop()
    {
        _cts.Cancel();
    }

    public void SetState(string state)
    {
        if ((state == ScanSessionStates.Counting || state == ScanSessionStates.Running) && _scanStartedAt == null)
        {
            _scanStartedAt = DateTimeOffset.UtcNow;
        }

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
            OutputPath,
            CalculateEta());
    }

    private string? CalculateEta()
    {
        if (_scanStartedAt is null)
        {
            return null;
        }

        if (TotalFiles is null || TotalFiles <= 0 || FilesScanned <= 0)
        {
            return null;
        }

        var remaining = TotalFiles.Value - FilesScanned;
        if (remaining <= 0)
        {
            return "00:00:00";
        }

        var elapsedSeconds = (DateTimeOffset.UtcNow - _scanStartedAt.Value).TotalSeconds;
        if (elapsedSeconds <= 0)
        {
            return null;
        }

        var filesPerSecond = FilesScanned / elapsedSeconds;
        if (filesPerSecond <= 0 || double.IsNaN(filesPerSecond) || double.IsInfinity(filesPerSecond))
        {
            return null;
        }

        var etaSeconds = remaining / filesPerSecond;
        if (double.IsNaN(etaSeconds) || double.IsInfinity(etaSeconds))
        {
            return null;
        }

        var boundedSeconds = Math.Max(0, (long)Math.Ceiling(etaSeconds));
        var eta = TimeSpan.FromSeconds(boundedSeconds);
        return $"{(int)eta.TotalHours:00}:{eta.Minutes:00}:{eta.Seconds:00}";
    }
}
