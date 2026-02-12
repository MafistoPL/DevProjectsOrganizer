using AppHost.Persistence;
using Microsoft.EntityFrameworkCore;

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

public sealed class ProjectSuggestionRegressionService
{
    private readonly Func<AppDbContext> _dbFactory;

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
}
