using AppHost.Persistence;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text.Json;

namespace AppHost.Services;

public sealed record ProjectSuggestionRegressionReport(
    Guid ScanSessionId,
    string RootPath,
    int BaselineAcceptedCount,
    int BaselineRejectedCount,
    int AcceptedMissingCount,
    int RejectedMissingCount,
    int AddedCount,
    IReadOnlyList<string> AcceptedMissingPaths,
    IReadOnlyList<string> RejectedMissingPaths
);

public sealed record ProjectSuggestionReplayRegressionRootReport(
    string RootPath,
    Guid SnapshotScanSessionId,
    string SnapshotPath,
    int BaselineAcceptedCount,
    int BaselineRejectedCount,
    int AcceptedMissingCount,
    int RejectedMissingCount,
    int AddedCount,
    IReadOnlyList<string> AcceptedMissingPaths,
    IReadOnlyList<string> RejectedMissingPaths
);

public sealed record ProjectSuggestionReplayRegressionReport(
    int RootsAnalyzed,
    int BaselineAcceptedCount,
    int BaselineRejectedCount,
    int AcceptedMissingCount,
    int RejectedMissingCount,
    int AddedCount,
    IReadOnlyList<ProjectSuggestionReplayRegressionRootReport> Roots
);

public sealed class ProjectSuggestionRegressionService
{
    private readonly Func<AppDbContext> _dbFactory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly ProjectSuggestionHeuristicsService _heuristics = new();

    public ProjectSuggestionRegressionService(Func<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<ProjectSuggestionRegressionReport> AnalyzeAsync(
        Guid currentScanSessionId,
        CancellationToken cancellationToken = default)
    {
        using var db = _dbFactory();

        var currentSession = await db.ScanSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == currentScanSessionId, cancellationToken);
        if (currentSession == null)
        {
            throw new InvalidOperationException("Scan session not found.");
        }

        var currentPaths = await db.ProjectSuggestions
            .AsNoTracking()
            .Where(item => item.ScanSessionId == currentScanSessionId)
            .Select(item => item.Path)
            .Distinct()
            .ToListAsync(cancellationToken);
        var currentPathSet = new HashSet<string>(currentPaths, StringComparer.OrdinalIgnoreCase);

        var historicalDecisions = await db.ProjectSuggestions
            .AsNoTracking()
            .Where(item =>
                item.RootPath == currentSession.RootPath
                && item.ScanSessionId != currentScanSessionId
                && item.Status != ProjectSuggestionStatus.Pending)
            .ToListAsync(cancellationToken);

        var latestByPath = historicalDecisions
            .OrderByDescending(item => item.CreatedAt)
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var acceptedMissing = latestByPath
            .Where(item =>
                item.Status == ProjectSuggestionStatus.Accepted
                && !currentPathSet.Contains(item.Path))
            .Select(item => item.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rejectedMissing = latestByPath
            .Where(item =>
                item.Status == ProjectSuggestionStatus.Rejected
                && !currentPathSet.Contains(item.Path))
            .Select(item => item.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var baselinePathSet = latestByPath
            .Select(item => item.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var addedCount = currentPathSet.Count(path => !baselinePathSet.Contains(path));

        return new ProjectSuggestionRegressionReport(
            currentScanSessionId,
            currentSession.RootPath,
            latestByPath.Count(item => item.Status == ProjectSuggestionStatus.Accepted),
            latestByPath.Count(item => item.Status == ProjectSuggestionStatus.Rejected),
            acceptedMissing.Count,
            rejectedMissing.Count,
            addedCount,
            acceptedMissing,
            rejectedMissing);
    }

    public async Task<ProjectSuggestionReplayRegressionReport> AnalyzeReplayFromHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        using var db = _dbFactory();

        var historicalDecisions = await db.ProjectSuggestions
            .AsNoTracking()
            .Where(item => item.Status != ProjectSuggestionStatus.Pending)
            .ToListAsync(cancellationToken);

        if (historicalDecisions.Count == 0)
        {
            throw new InvalidOperationException(
                "No accepted/rejected suggestions found in database. Build baseline via UI decisions first.");
        }

        var decisionsByScan = historicalDecisions
            .GroupBy(item => item.ScanSessionId)
            .ToList();

        var rootReports = new List<ProjectSuggestionReplayRegressionRootReport>();
        foreach (var scanGroup in decisionsByScan)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scanSessionId = scanGroup.Key;
            var baselineByPath = scanGroup
                .OrderByDescending(item => item.CreatedAt)
                .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            var scanSession = await db.ScanSessions
                .AsNoTracking()
                .Where(item => item.Id == scanSessionId && item.OutputPath != null)
                .FirstOrDefaultAsync(cancellationToken);

            if (scanSession == null || string.IsNullOrWhiteSpace(scanSession.OutputPath))
            {
                continue;
            }

            var snapshotPath = scanSession.OutputPath;
            if (!File.Exists(snapshotPath))
            {
                continue;
            }

            var snapshot = await LoadSnapshotAsync(snapshotPath, cancellationToken);
            var recomputedPaths = _heuristics.Detect(snapshot)
                .Select(item => item.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var acceptedMissing = baselineByPath
                .Where(item =>
                    item.Status == ProjectSuggestionStatus.Accepted
                    && !recomputedPaths.Contains(item.Path))
                .Select(item => item.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rejectedMissing = baselineByPath
                .Where(item =>
                    item.Status == ProjectSuggestionStatus.Rejected
                    && !recomputedPaths.Contains(item.Path))
                .Select(item => item.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var baselinePathSet = baselineByPath
                .Select(item => item.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var addedCount = recomputedPaths.Count(path => !baselinePathSet.Contains(path));

            rootReports.Add(new ProjectSuggestionReplayRegressionRootReport(
                scanSession.RootPath,
                scanSession.Id,
                snapshotPath,
                baselineByPath.Count(item => item.Status == ProjectSuggestionStatus.Accepted),
                baselineByPath.Count(item => item.Status == ProjectSuggestionStatus.Rejected),
                acceptedMissing.Count,
                rejectedMissing.Count,
                addedCount,
                acceptedMissing,
                rejectedMissing));
        }

        if (rootReports.Count == 0)
        {
            throw new InvalidOperationException(
                "No historical scan snapshots were found for roots with user decisions.");
        }

        return new ProjectSuggestionReplayRegressionReport(
            rootReports.Count,
            rootReports.Sum(item => item.BaselineAcceptedCount),
            rootReports.Sum(item => item.BaselineRejectedCount),
            rootReports.Sum(item => item.AcceptedMissingCount),
            rootReports.Sum(item => item.RejectedMissingCount),
            rootReports.Sum(item => item.AddedCount),
            rootReports);
    }

    private async Task<ScanSnapshot> LoadSnapshotAsync(string path, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var snapshot = JsonSerializer.Deserialize<ScanSnapshot>(json, _jsonOptions);
        if (snapshot == null)
        {
            throw new InvalidOperationException($"Cannot deserialize scan snapshot: {path}");
        }

        return snapshot;
    }
}
