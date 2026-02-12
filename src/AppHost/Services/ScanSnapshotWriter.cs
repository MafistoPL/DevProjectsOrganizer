using System.Text.Json;
using System.IO;
using AppHost.Persistence;

namespace AppHost.Services;

public sealed class ScanSnapshotWriter
{
    public async Task<string> SaveAsync(ScanSnapshot snapshot, Guid scanId, CancellationToken cancellationToken)
    {
        var dataDir = Path.GetDirectoryName(AppDbContext.GetDefaultDbPath())!;
        var scansDir = Path.Combine(dataDir, "scans");
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
