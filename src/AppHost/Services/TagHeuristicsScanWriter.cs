using System.IO;
using System.Text.Json;
using AppHost.Persistence;

namespace AppHost.Services;

public sealed class TagHeuristicsScanWriter
{
    private readonly string _dataDirectory;

    public TagHeuristicsScanWriter(string? dataDirectory = null)
    {
        _dataDirectory = dataDirectory ?? Path.GetDirectoryName(AppDbContext.GetDefaultDbPath())!;
    }

    public async Task<string> SaveAsync(
        TagHeuristicsScanSnapshot snapshot,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var scansDir = Path.Combine(_dataDirectory, "scans");
        Directory.CreateDirectory(scansDir);

        var outputPath = Path.Combine(scansDir, $"scan-tag-heur-{runId:D}.json");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
        return outputPath;
    }
}
