using Engine.Scanning;
using System.IO;

namespace AppHost.Services;

public sealed class ScanSnapshotBuilder
{
    public async Task<long> CountFilesAsync(ScanRuntime runtime)
    {
        var count = 0L;
        foreach (var root in runtime.GetRootPaths())
        {
            count += await CountFilesAsync(root, 0, runtime);
        }

        return count;
    }

    public async Task<ScanSnapshot> BuildSnapshotAsync(
        ScanRuntime runtime,
        Func<ScanRuntime, Task> reportProgressAsync)
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
            var node = await BuildDirectoryNodeAsync(root, 0, runtime, reportProgressAsync);
            snapshot.Roots.Add(node);
        }

        snapshot.FilesScanned = runtime.FilesScanned;
        snapshot.FinishedAt = DateTimeOffset.UtcNow;
        return snapshot;
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

                continue;
            }

            var fileName = Path.GetFileName(entry);
            var extension = Path.GetExtension(entry);
            if (ScanIgnorePolicy.ShouldSkipFile(fileName, extension))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private async Task<DirectoryNode> BuildDirectoryNodeAsync(
        string path,
        int depth,
        ScanRuntime runtime,
        Func<ScanRuntime, Task> reportProgressAsync)
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
                    node.Directories.Add(
                        await BuildDirectoryNodeAsync(entry, depth + 1, runtime, reportProgressAsync));
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

            var fileNode = await BuildFileNodeAsync(entry, runtime, reportProgressAsync);
            node.Files.Add(fileNode);
        }

        return node;
    }

    private async Task<FileNode> BuildFileNodeAsync(
        string path,
        ScanRuntime runtime,
        Func<ScanRuntime, Task> reportProgressAsync)
    {
        await runtime.WaitIfPausedAsync();
        runtime.StopToken.ThrowIfCancellationRequested();

        runtime.FilesScanned++;
        runtime.CurrentPath = path;

        if (runtime.ShouldReportProgress())
        {
            await reportProgressAsync(runtime);
        }

        var fileInfo = new FileInfo(path);
        var fileNode = new FileNode
        {
            Name = fileInfo.Name,
            Path = fileInfo.FullName,
            Extension = fileInfo.Extension,
            Size = fileInfo.Exists ? fileInfo.Length : 0
        };

        if (SamplePolicy.ShouldSample(fileInfo.Name, fileInfo.Extension))
        {
            var (lines, truncated) = await ReadSampleLinesAsync(fileInfo, runtime);
            fileNode.SampleLines = lines;
            fileNode.SampleTruncated = truncated;
        }

        return fileNode;
    }

    private static async Task<(IReadOnlyList<string> lines, bool truncated)> ReadSampleLinesAsync(
        FileInfo fileInfo,
        ScanRuntime runtime)
    {
        const int maxLines = 100;
        const int maxChars = 8192;

        var lines = new List<string>();
        var totalChars = 0;

        try
        {
            using var stream = fileInfo.OpenRead();
            using var reader = new StreamReader(stream);
            for (var i = 0; i < maxLines; i++)
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
}
