using AppHost.Persistence;
using Engine.Models;
using Microsoft.EntityFrameworkCore;

namespace AppHost.Services;

public sealed record RootOverviewDto(
    Guid Id,
    string Path,
    string Status,
    int ProjectCount,
    int OngoingSuggestionCount,
    string? LastScanState,
    DateTimeOffset? LastScanAt,
    long? LastScanFiles
);

public sealed class RootOverviewService
{
    private readonly AppDbContext _db;

    public RootOverviewService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RootOverviewDto>> BuildAsync(
        IReadOnlyList<Root> roots,
        CancellationToken cancellationToken = default)
    {
        if (roots.Count == 0)
        {
            return Array.Empty<RootOverviewDto>();
        }

        var rootPaths = roots.Select(root => root.Path).ToList();

        var suggestionRows = await _db.ProjectSuggestions
            .AsNoTracking()
            .Where(item => rootPaths.Contains(item.RootPath))
            .Select(item => new { item.RootPath, item.Status })
            .ToListAsync(cancellationToken);

        var scanRows = await _db.ScanSessions
            .AsNoTracking()
            .Where(item => rootPaths.Contains(item.RootPath))
            .Select(item => new
            {
                item.RootPath,
                item.CreatedAt,
                item.State,
                LastAt = item.FinishedAt ?? item.StartedAt ?? item.CreatedAt,
                item.FilesScanned
            })
            .ToListAsync(cancellationToken);

        var countsByRoot = suggestionRows
            .GroupBy(item => item.RootPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    ProjectCount = group.Count(),
                    Ongoing = group.Count(item => item.Status == ProjectSuggestionStatus.Pending)
                },
                StringComparer.OrdinalIgnoreCase);

        var latestScanByRoot = scanRows
            .GroupBy(item => item.RootPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.CreatedAt).First(),
                StringComparer.OrdinalIgnoreCase);

        return roots.Select(root =>
        {
            countsByRoot.TryGetValue(root.Path, out var counts);
            latestScanByRoot.TryGetValue(root.Path, out var latestScan);

            return new RootOverviewDto(
                root.Id,
                root.Path,
                root.Status,
                counts?.ProjectCount ?? 0,
                counts?.Ongoing ?? 0,
                latestScan?.State,
                latestScan?.LastAt,
                latestScan?.FilesScanned);
        }).ToList();
    }
}
