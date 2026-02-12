using System.Text.Json;
using System.IO;
using AppHost.Persistence;

namespace AppHost.Services;

public sealed class ScanSnapshotWriter
{
    private readonly string _dataDirectory;

    public ScanSnapshotWriter(string? dataDirectory = null)
    {
        _dataDirectory = dataDirectory ?? Path.GetDirectoryName(AppDbContext.GetDefaultDbPath())!;
    }

    public async Task<string> SaveAsync(ScanSnapshot snapshot, Guid scanId, CancellationToken cancellationToken)
    {
        var scansDir = Path.Combine(_dataDirectory, "scans");
        Directory.CreateDirectory(scansDir);

        var outputPath = Path.Combine(scansDir, $"scan-{scanId}.json");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
        return outputPath;
    }
}
