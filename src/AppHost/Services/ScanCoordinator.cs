using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using AppHost.Persistence;

namespace AppHost.Services;

public sealed record ScanSessionDto(
    Guid Id,
    string RootPath,
    string Mode,
    string State,
    string Disk,
    string? CurrentPath,
    long FilesScanned,
    long? TotalFiles,
    string? QueueReason,
    string? OutputPath
);

public sealed record ScanStartRequest(
    string Mode,
    Guid? RootId,
    int? DepthLimit
);

public sealed class ScanCoordinator
{
    private readonly Func<AppDbContext> _dbFactory;
    private readonly ScanExecutionService _executionService;
    private readonly ConcurrentDictionary<Guid, ScanRuntime> _scans = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _diskLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _wholeLock = new(1, 1);

    public event Action<string, object?>? ScanEvent;

    public ScanCoordinator(
        Func<AppDbContext> dbFactory,
        ScanExecutionService? executionService = null)
    {
        _dbFactory = dbFactory;
        _executionService = executionService ?? new ScanExecutionService();
    }

    public async Task<ScanSessionDto> StartAsync(ScanStartRequest request, CancellationToken cancellationToken)
    {
        var (rootId, rootPath, diskKey) = await ResolveRootAsync(request, cancellationToken);
        var session = new ScanSessionEntity
        {
            Id = Guid.NewGuid(),
            RootId = rootId,
            RootPath = rootPath,
            Mode = request.Mode,
            State = ScanSessionStates.Queued,
            DiskKey = diskKey,
            CreatedAt = DateTimeOffset.UtcNow,
            DepthLimit = request.DepthLimit
        };

        using (var db = _dbFactory())
        {
            db.ScanSessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);
        }

        var runtime = new ScanRuntime(session.Id, rootPath, diskKey, request);
        _scans[session.Id] = runtime;

        _ = Task.Run(() => RunScanAsync(runtime), CancellationToken.None);

        var dto = ToDto(session, runtime.QueueReason);
        EmitProgress(dto);
        return dto;
    }

    public Task<IReadOnlyList<ScanSessionDto>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var sessions = _scans.Values.Select(runtime => runtime.ToDto()).ToList();
        return Task.FromResult<IReadOnlyList<ScanSessionDto>>(sessions);
    }

    public Task PauseAsync(Guid scanId)
    {
        if (_scans.TryGetValue(scanId, out var runtime))
        {
            runtime.Pause();
            _ = UpdateStateAsync(runtime.ScanId, runtime.State, runtime);
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync(Guid scanId)
    {
        if (_scans.TryGetValue(scanId, out var runtime))
        {
            runtime.Resume();
            _ = UpdateStateAsync(runtime.ScanId, runtime.State, runtime);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(Guid scanId)
    {
        if (_scans.TryGetValue(scanId, out var runtime))
        {
            runtime.SetState(ScanSessionStates.Stopped);
            runtime.Stop();
            _ = UpdateStateAsync(runtime.ScanId, runtime.State, runtime);
        }

        return Task.CompletedTask;
    }

    private async Task RunScanAsync(ScanRuntime runtime)
    {
        var diskLock = _diskLocks.GetOrAdd(runtime.DiskKey, _ => new SemaphoreSlim(1, 1));
        var diskLockHeld = false;
        var wholeLockHeld = false;

        try
        {
            if (runtime.Request.Mode != "whole" && _wholeLock.CurrentCount == 0)
            {
                runtime.QueueReason = "Waiting for whole-computer scan";
                await UpdateStateAsync(runtime.ScanId, ScanSessionStates.Queued, runtime);
            }

            if (runtime.Request.Mode == "whole")
            {
                await _wholeLock.WaitAsync(runtime.StopToken);
                wholeLockHeld = true;
                runtime.HoldsWholeLock = true;
                await WaitForOtherScansAsync(runtime);
            }
            else
            {
                await _wholeLock.WaitAsync(runtime.StopToken);
                _wholeLock.Release();
            }

            if (diskLock.CurrentCount == 0)
            {
                runtime.QueueReason = $"Waiting for disk {runtime.DiskKey}";
                await UpdateStateAsync(runtime.ScanId, ScanSessionStates.Queued, runtime);
            }

            await diskLock.WaitAsync(runtime.StopToken);
            diskLockHeld = true;
            runtime.QueueReason = null;

            var outputPath = await _executionService.ExecuteAsync(runtime, ReportStateAsync);
            runtime.SetState(ScanSessionStates.Completed);
            runtime.OutputPath = outputPath;
            await UpdateStateAsync(runtime.ScanId, ScanSessionStates.Completed, runtime);
            Emit(ScanEventTypes.Completed, new { id = runtime.ScanId, outputPath });
        }
        catch (OperationCanceledException)
        {
            runtime.SetState(ScanSessionStates.Stopped);
            await UpdateStateAsync(runtime.ScanId, ScanSessionStates.Stopped, runtime);
            Emit(ScanEventTypes.Completed, new { id = runtime.ScanId });
        }
        catch (Exception ex)
        {
            runtime.SetState(ScanSessionStates.Failed);
            await UpdateStateAsync(runtime.ScanId, ScanSessionStates.Failed, runtime);
            Emit(ScanEventTypes.Failed, new { id = runtime.ScanId, error = ex.Message });
        }
        finally
        {
            if (diskLockHeld)
            {
                diskLock.Release();
            }

            if (wholeLockHeld || runtime.HoldsWholeLock)
            {
                _wholeLock.Release();
                runtime.HoldsWholeLock = false;
            }

            if (ScanSessionStates.IsTerminal(runtime.State))
            {
                _scans.TryRemove(runtime.ScanId, out _);
            }
        }
    }

    private async Task ReportStateAsync(ScanRuntime runtime)
    {
        await UpdateStateAsync(runtime.ScanId, runtime.State, runtime);
    }

    private async Task WaitForOtherScansAsync(ScanRuntime runtime)
    {
        while (true)
        {
            runtime.StopToken.ThrowIfCancellationRequested();

            var others = _scans.Values.Where(scan => scan.ScanId != runtime.ScanId).ToList();
            var blocking = others.Any(scan =>
                scan.State is ScanSessionStates.Running or ScanSessionStates.Counting or ScanSessionStates.Paused
                || (scan.State == ScanSessionStates.Queued
                    && scan.QueueReason != "Waiting for whole-computer scan"));

            if (!blocking)
            {
                runtime.QueueReason = null;
                return;
            }

            runtime.QueueReason = "Waiting for other scans to finish";
            await UpdateStateAsync(runtime.ScanId, ScanSessionStates.Queued, runtime);
            await Task.Delay(500, runtime.StopToken);
        }
    }

    private async Task UpdateStateAsync(Guid scanId, string state, ScanRuntime runtime)
    {
        using var db = _dbFactory();
        var session = await db.ScanSessions.FindAsync(scanId);
        if (session == null)
        {
            return;
        }

        session.State = state;
        session.FilesScanned = runtime.FilesScanned;
        session.TotalFiles = runtime.TotalFiles;
        session.CurrentPath = runtime.CurrentPath;
        session.OutputPath = runtime.OutputPath;
        if (state is ScanSessionStates.Counting or ScanSessionStates.Running or ScanSessionStates.Paused)
        {
            session.StartedAt ??= DateTimeOffset.UtcNow;
        }

        if (ScanSessionStates.IsTerminal(state))
        {
            session.FinishedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        EmitProgress(runtime.ToDto());
    }

    private async Task<(Guid? rootId, string rootPath, string diskKey)> ResolveRootAsync(
        ScanStartRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Mode == "whole")
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            return (null, systemDrive, "ALL");
        }

        if (request.RootId == null)
        {
            throw new InvalidOperationException("Root id is required for this scan mode.");
        }

        using var db = _dbFactory();
        var root = await db.Roots.FindAsync(new object?[] { request.RootId.Value }, cancellationToken);
        if (root == null)
        {
            throw new InvalidOperationException("Root not found.");
        }

        var diskKey = GetDiskKey(root.Path);
        return (root.Id, root.Path, diskKey);
    }

    private static string GetDiskKey(string path)
    {
        var root = Path.GetPathRoot(path) ?? path;
        return root.TrimEnd(Path.DirectorySeparatorChar);
    }

    private void EmitProgress(ScanSessionDto dto)
    {
        Emit(ScanEventTypes.Progress, dto);
    }

    private static ScanSessionDto ToDto(ScanSessionEntity session, string? queueReason)
    {
        return new ScanSessionDto(
            session.Id,
            session.RootPath,
            session.Mode,
            session.State,
            session.DiskKey,
            session.CurrentPath,
            session.FilesScanned,
            session.TotalFiles,
            queueReason,
            session.OutputPath);
    }

    private void Emit(string type, object? data)
    {
        ScanEvent?.Invoke(type, data);
    }
}
