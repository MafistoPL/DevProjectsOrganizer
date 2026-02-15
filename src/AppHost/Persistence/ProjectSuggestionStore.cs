using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace AppHost.Persistence;

public sealed class ProjectSuggestionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;

    public ProjectSuggestionStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task ReplaceForScanAsync(
        Guid scanSessionId,
        IReadOnlyList<Services.DetectedProjectSuggestion> suggestions,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ScanSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == scanSessionId, cancellationToken);
        if (session == null)
        {
            throw new InvalidOperationException("Scan session not found.");
        }

        var existing = await _db.ProjectSuggestions
            .Where(item => item.ScanSessionId == scanSessionId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            _db.ProjectSuggestions.RemoveRange(existing);
        }

        var filteredByRejections = await FilterByHistoricalRejectionsAsync(suggestions, cancellationToken);
        var filteredByAccepted = await FilterByHistoricalAcceptedAsync(filteredByRejections, cancellationToken);
        var filteredSuggestions = await FilterAlreadyMaterializedProjectsAsync(
            filteredByAccepted,
            cancellationToken);

        var entities = filteredSuggestions.Select(item => new ProjectSuggestionEntity
        {
            Id = Guid.NewGuid(),
            ScanSessionId = scanSessionId,
            RootPath = session.RootPath,
            Name = item.Name,
            Path = item.Path,
            Kind = item.Kind,
            Score = item.Score,
            Reason = item.Reason,
            ExtensionsSummary = item.ExtensionsSummary,
            Fingerprint = item.Fingerprint,
            MarkersJson = JsonSerializer.Serialize(item.Markers, JsonOptions),
            TechHintsJson = JsonSerializer.Serialize(item.TechHints, JsonOptions),
            CreatedAt = item.CreatedAt,
            Status = ProjectSuggestionStatus.Pending
        });

        _db.ProjectSuggestions.AddRange(entities);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectSuggestionEntity>> GetByScanAsync(
        Guid scanSessionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.ProjectSuggestions
            .AsNoTracking()
            .Where(item => item.ScanSessionId == scanSessionId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectSuggestionEntity>> ListAllAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await _db.ProjectSuggestions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var latestDecisionByPath = items
            .Where(item => item.Status != ProjectSuggestionStatus.Pending)
            .GroupBy(item => BuildPathKey(item.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.CreatedAt)
                .First())
            .ToDictionary(
                item => BuildPathKey(item.Path),
                item => item.Status,
                StringComparer.OrdinalIgnoreCase);

        var existingProjectPathKeys = await _db.Projects
            .AsNoTracking()
            .Select(item => item.Path)
            .ToListAsync(cancellationToken);

        var materializedPathKeys = existingProjectPathKeys
            .Select(BuildPathKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filtered = items
            .Where(item =>
            {
                if (item.Status != ProjectSuggestionStatus.Pending)
                {
                    return true;
                }

                var pathKey = BuildPathKey(item.Path);
                if (materializedPathKeys.Contains(pathKey))
                {
                    return false;
                }

                if (!latestDecisionByPath.TryGetValue(pathKey, out var latestStatus))
                {
                    return true;
                }

                return latestStatus != ProjectSuggestionStatus.Accepted;
            });

        return filtered
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
    }

    public async Task<ProjectSuggestionEntity?> GetByIdAsync(
        Guid suggestionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.ProjectSuggestions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == suggestionId, cancellationToken);
    }

    public async Task<ProjectSuggestionEntity> SetStatusAsync(
        Guid suggestionId,
        ProjectSuggestionStatus status,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ProjectSuggestions
            .FirstOrDefaultAsync(item => item.Id == suggestionId, cancellationToken);
        if (entity == null)
        {
            throw new InvalidOperationException("Project suggestion not found.");
        }

        entity.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<bool> DeleteAsync(
        Guid suggestionId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ProjectSuggestions
            .FirstOrDefaultAsync(item => item.Id == suggestionId, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        _db.ProjectSuggestions.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<IReadOnlyList<Services.DetectedProjectSuggestion>> FilterByHistoricalRejectionsAsync(
        IReadOnlyList<Services.DetectedProjectSuggestion> suggestions,
        CancellationToken cancellationToken)
    {
        if (suggestions.Count == 0)
        {
            return suggestions;
        }

        var candidatePathKindKeys = suggestions
            .Select(item => BuildPathKindKey(item.Path, item.Kind))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var historicalDecisions = await _db.ProjectSuggestions
            .AsNoTracking()
            .Where(item => item.Status != ProjectSuggestionStatus.Pending)
            .Select(item => new
            {
                item.Path,
                item.Kind,
                item.Fingerprint,
                item.Status,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var rejectedKeys = historicalDecisions
            .Where(item => candidatePathKindKeys.Contains(BuildPathKindKey(item.Path, item.Kind)))
            .GroupBy(
                item => BuildDecisionKey(item.Path, item.Kind, item.Fingerprint),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.CreatedAt)
                .First())
            .Where(item => item.Status == ProjectSuggestionStatus.Rejected)
            .Select(item => BuildDecisionKey(item.Path, item.Kind, item.Fingerprint))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return suggestions
            .Where(item => !rejectedKeys.Contains(BuildDecisionKey(item.Path, item.Kind, item.Fingerprint)))
            .ToList();
    }

    private async Task<IReadOnlyList<Services.DetectedProjectSuggestion>> FilterByHistoricalAcceptedAsync(
        IReadOnlyList<Services.DetectedProjectSuggestion> suggestions,
        CancellationToken cancellationToken)
    {
        if (suggestions.Count == 0)
        {
            return suggestions;
        }

        var candidatePathKeys = suggestions
            .Select(item => BuildPathKey(item.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var historicalDecisions = await _db.ProjectSuggestions
            .AsNoTracking()
            .Where(item => item.Status != ProjectSuggestionStatus.Pending)
            .Select(item => new
            {
                item.Path,
                item.Status,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var acceptedPathKeys = historicalDecisions
            .Where(item => candidatePathKeys.Contains(BuildPathKey(item.Path)))
            .GroupBy(item => BuildPathKey(item.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.CreatedAt)
                .First())
            .Where(item => item.Status == ProjectSuggestionStatus.Accepted)
            .Select(item => BuildPathKey(item.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return suggestions
            .Where(item => !acceptedPathKeys.Contains(BuildPathKey(item.Path)))
            .ToList();
    }

    private async Task<IReadOnlyList<Services.DetectedProjectSuggestion>> FilterAlreadyMaterializedProjectsAsync(
        IReadOnlyList<Services.DetectedProjectSuggestion> suggestions,
        CancellationToken cancellationToken)
    {
        if (suggestions.Count == 0)
        {
            return suggestions;
        }

        var candidatePathKeys = suggestions
            .Select(item => BuildPathKey(item.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingProjectPaths = await _db.Projects
            .AsNoTracking()
            .Select(item => item.Path)
            .ToListAsync(cancellationToken);

        var existingPathKeys = existingProjectPaths
            .Select(BuildPathKey)
            .Where(candidatePathKeys.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (existingPathKeys.Count == 0)
        {
            return suggestions;
        }

        return suggestions
            .Where(item => !existingPathKeys.Contains(BuildPathKey(item.Path)))
            .ToList();
    }

    private static string BuildDecisionKey(string path, string kind, string fingerprint)
    {
        return $"{BuildPathKindKey(path, kind)}::{NormalizeFingerprint(fingerprint)}";
    }

    private static string BuildPathKindKey(string path, string kind)
    {
        return $"{BuildPathKey(path)}::{NormalizeKind(kind)}";
    }

    private static string BuildPathKey(string path)
    {
        return NormalizePath(path);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path
            .Trim()
            .Replace('\\', '/')
            .TrimEnd('/')
            .ToLowerInvariant();
    }

    private static string NormalizeKind(string kind)
    {
        return kind.Trim().ToLowerInvariant();
    }

    private static string NormalizeFingerprint(string fingerprint)
    {
        return fingerprint.Trim().ToLowerInvariant();
    }
}
