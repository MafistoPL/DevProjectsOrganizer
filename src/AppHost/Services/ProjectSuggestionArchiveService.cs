using System.IO;
using System.Text.Json;
using AppHost.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppHost.Services;

public sealed record ProjectSuggestionArchiveExportResult(string Path, int Count);

public sealed class ProjectSuggestionArchiveService
{
    private readonly Func<AppDbContext> _dbFactory;
    private readonly string _exportDirectory;

    public ProjectSuggestionArchiveService(Func<AppDbContext> dbFactory, string? exportDirectory = null)
    {
        _dbFactory = dbFactory;
        _exportDirectory = exportDirectory ?? GetDefaultExportDirectory();
    }

    public string EnsureArchiveDirectory()
    {
        Directory.CreateDirectory(_exportDirectory);
        return _exportDirectory;
    }

    public async Task<ProjectSuggestionArchiveExportResult> ExportAsync(
        CancellationToken cancellationToken = default)
    {
        using var db = _dbFactory();

        var items = await db.ProjectSuggestions
            .AsNoTracking()
            .Where(item => item.Status != ProjectSuggestionStatus.Pending)
            .ToListAsync(cancellationToken);

        var ordered = items
            .OrderByDescending(item => item.CreatedAt)
            .ToList();

        var payload = new
        {
            exportedAt = DateTimeOffset.UtcNow,
            count = ordered.Count,
            suggestions = ordered.Select(item => new
            {
                item.Id,
                item.ScanSessionId,
                item.RootPath,
                item.Name,
                item.Path,
                item.Kind,
                item.Score,
                item.Reason,
                item.ExtensionsSummary,
                markers = DeserializeStringList(item.MarkersJson),
                techHints = DeserializeStringList(item.TechHintsJson),
                status = item.Status.ToString(),
                item.CreatedAt
            })
        };

        Directory.CreateDirectory(_exportDirectory);
        var path = Path.Combine(
            _exportDirectory,
            $"suggestions-archive-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return new ProjectSuggestionArchiveExportResult(path, ordered.Count);
    }

    public static string GetDefaultExportDirectory()
    {
        var appDir = Path.GetDirectoryName(AppDbContext.GetDefaultDbPath())!;
        return Path.Combine(appDir, "exports");
    }

    private static IReadOnlyList<string> DeserializeStringList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
