using System.Collections.Concurrent;
using System.Text.Json;
using AppHost.Persistence;
using Engine.Scanning;
using System.IO;
using System.Linq;

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
    private readonly ConcurrentDictionary<Guid, ScanRuntime> _scans = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _diskLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _wholeLock = new(1, 1);

    public event Action<string, object?>? ScanEvent;

    public ScanCoordinator(Func<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
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
            State = "Queued",
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
            runtime.SetState("Stopped");
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
                await UpdateStateAsync(runtime.ScanId, "Queued", runtime);
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
                await UpdateStateAsync(runtime.ScanId, "Queued", runtime);
            }

            await diskLock.WaitAsync(runtime.StopToken);
            diskLockHeld = true;
            runtime.QueueReason = null;

            var shouldCount = runtime.Request.Mode != "whole";
            if (shouldCount)
            {
                runtime.SetState("Counting");
                await UpdateStateAsync(runtime.ScanId, "Counting", runtime);
                runtime.TotalFiles = await CountFilesAsync(runtime);
            }

            runtime.SetState("Running");
            await UpdateStateAsync(runtime.ScanId, "Running", runtime);

            var snapshot = await BuildSnapshotAsync(runtime);
            var outputPath = await SaveSnapshotAsync(snapshot, runtime);

            runtime.SetState("Completed");
            runtime.OutputPath = outputPath;
            await UpdateStateAsync(runtime.ScanId, "Completed", runtime);
            Emit("scan.completed", new { id = runtime.ScanId, outputPath });
        }
        catch (OperationCanceledException)
        {
            runtime.SetState("Stopped");
            await UpdateStateAsync(runtime.ScanId, "Stopped", runtime);
            Emit("scan.completed", new { id = runtime.ScanId });
        }
        catch (Exception ex)
        {
            runtime.SetState("Failed");
            await UpdateStateAsync(runtime.ScanId, "Failed", runtime);
            Emit("scan.failed", new { id = runtime.ScanId, error = ex.Message });
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
            if (runtime.State is "Completed" or "Failed" or "Stopped")
            {
                _scans.TryRemove(runtime.ScanId, out _);
            }
        }
    }

    private async Task WaitForOtherScansAsync(ScanRuntime runtime)
    {
        while (true)
        {
            runtime.StopToken.ThrowIfCancellationRequested();

            var others = _scans.Values.Where(scan => scan.ScanId != runtime.ScanId).ToList();
            var blocking = others.Any(scan =>
                scan.State is "Running" or "Counting" or "Paused"
                || (scan.State == "Queued" && scan.QueueReason != "Waiting for whole-computer scan"));

            if (!blocking)
            {
                runtime.QueueReason = null;
                return;
            }

            runtime.QueueReason = "Waiting for other scans to finish";
            await UpdateStateAsync(runtime.ScanId, "Queued", runtime);
            await Task.Delay(500, runtime.StopToken);
        }
    }

    private async Task<long> CountFilesAsync(ScanRuntime runtime)
    {
        var count = 0L;
        foreach (var root in runtime.GetRootPaths())
        {
            count += await CountFilesAsync(root, 0, runtime);
        }
        return count;
    }

    private async Task<long> CountFilesAsync(string path, int depth, ScanRuntime runtime)
    {
        await runtime.WaitIfPausedAsync();
        runtime.StopToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(path))
        {
            return 0;
        }

        long count = 0;
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(path);
        }
        catch
        {
            return 0;
        }

        foreach (var entry in entries)
        {
            await runtime.WaitIfPausedAsync();
            runtime.StopToken.ThrowIfCancellationRequested();

            if (Directory.Exists(entry))
            {
                var directoryName = Path.GetFileName(entry.TrimEnd(Path.DirectorySeparatorChar));
                if (ScanIgnorePolicy.ShouldSkipDirectory(directoryName))
                {
                    continue;
                }

                if (runtime.HasDepthRemaining(depth))
                {
                    count += await CountFilesAsync(entry, depth + 1, runtime);
                }
            }
            else
            {
                var fileName = Path.GetFileName(entry);
                var extension = Path.GetExtension(entry);
                if (ScanIgnorePolicy.ShouldSkipFile(fileName, extension))
                {
                    continue;
                }

                count++;
            }
        }

        return count;
    }

    private async Task<ScanSnapshot> BuildSnapshotAsync(ScanRuntime runtime)
    {
        var snapshot = new ScanSnapshot
        {
            ScanId = runtime.ScanId,
            Mode = runtime.Request.Mode,
            StartedAt = DateTimeOffset.UtcNow,
            DepthLimit = runtime.Request.DepthLimit,
            TotalFiles = runtime.TotalFiles
        };

        foreach (var root in runtime.GetRootPaths())
        {
            var node = await BuildDirectoryNodeAsync(root, 0, runtime);
            snapshot.Roots.Add(node);
        }

        snapshot.FilesScanned = runtime.FilesScanned;
        snapshot.FinishedAt = DateTimeOffset.UtcNow;
        return snapshot;
    }

    private async Task<DirectoryNode> BuildDirectoryNodeAsync(string path, int depth, ScanRuntime runtime)
    {
        await runtime.WaitIfPausedAsync();
        runtime.StopToken.ThrowIfCancellationRequested();

        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name))
        {
            name = path;
        }
        var node = new DirectoryNode
        {
            Name = name,
            Path = path
        };

        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(path);
        }
        catch
        {
            return node;
        }

        foreach (var entry in entries)
        {
            await runtime.WaitIfPausedAsync();
            runtime.StopToken.ThrowIfCancellationRequested();

            if (Directory.Exists(entry))
            {
                var directoryName = Path.GetFileName(entry.TrimEnd(Path.DirectorySeparatorChar));
                if (ScanIgnorePolicy.ShouldSkipDirectory(directoryName))
                {
                    continue;
                }

                if (runtime.HasDepthRemaining(depth))
                {
                    node.Directories.Add(await BuildDirectoryNodeAsync(entry, depth + 1, runtime));
                }
                else
                {
                    node.Directories.Add(new DirectoryNode
                    {
                        Name = Path.GetFileName(entry),
                        Path = entry
                    });
                }
                continue;
            }

            var fileName = Path.GetFileName(entry);
            var extension = Path.GetExtension(entry);
            if (ScanIgnorePolicy.ShouldSkipFile(fileName, extension))
            {
                continue;
            }

            var fileNode = await BuildFileNodeAsync(entry, runtime);
            node.Files.Add(fileNode);
        }

        return node;
    }

    private async Task<FileNode> BuildFileNodeAsync(string path, ScanRuntime runtime)
    {
        await runtime.WaitIfPausedAsync();
        runtime.StopToken.ThrowIfCancellationRequested();

        runtime.FilesScanned++;
        runtime.CurrentPath = path;

        if (runtime.ShouldReportProgress())
        {
            await UpdateStateAsync(runtime.ScanId, runtime.State, runtime);
        }

        var fileInfo = new FileInfo(path);
        var extension = fileInfo.Extension;
        var fileNode = new FileNode
        {
            Name = fileInfo.Name,
            Path = fileInfo.FullName,
            Extension = extension,
            Size = fileInfo.Exists ? fileInfo.Length : 0
        };

        if (ShouldSampleFile(fileInfo))
        {
            var (lines, truncated) = await ReadSampleLinesAsync(fileInfo, runtime);
            fileNode.SampleLines = lines;
            fileNode.SampleTruncated = truncated;
        }

        return fileNode;
    }

    private static bool ShouldSampleFile(FileInfo fileInfo)
    {
        return SamplePolicy.ShouldSample(fileInfo.Name, fileInfo.Extension);
    }

    private static async Task<(IReadOnlyList<string> lines, bool truncated)> ReadSampleLinesAsync(
        FileInfo fileInfo,
        ScanRuntime runtime)
    {
        const int maxLines = 30;
        const int maxChars = 8192;

        var lines = new List<string>();
        var totalChars = 0;

        try
        {
            using var stream = fileInfo.OpenRead();
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream && lines.Count < maxLines)
            {
                runtime.StopToken.ThrowIfCancellationRequested();
                await runtime.WaitIfPausedAsync();

                var line = await reader.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                totalChars += line.Length;
                if (totalChars > maxChars)
                {
                    return (lines, true);
                }

                lines.Add(line);
            }
        }
        catch
        {
            return (Array.Empty<string>(), false);
        }

        return (lines, false);
    }

    private async Task<string> SaveSnapshotAsync(ScanSnapshot snapshot, ScanRuntime runtime)
    {
        var dataDir = Path.GetDirectoryName(AppDbContext.GetDefaultDbPath())!;
        var scansDir = Path.Combine(dataDir, "scans");
        Directory.CreateDirectory(scansDir);
        var fileName = $"scan-{runtime.ScanId}.json";
        var outputPath = Path.Combine(scansDir, fileName);

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(outputPath, json, runtime.StopToken);
        return outputPath;
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
        if (state is "Counting" or "Running" or "Paused")
        {
            session.StartedAt ??= DateTimeOffset.UtcNow;
        }
        if (state is "Completed" or "Failed" or "Stopped")
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
        Emit("scan.progress", dto);
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

    private sealed class ScanRuntime
    {
        private readonly AsyncManualResetEvent _pauseEvent = new(true);
        private DateTimeOffset _lastProgressAt = DateTimeOffset.MinValue;

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

        private readonly CancellationTokenSource _cts = new();

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
}
