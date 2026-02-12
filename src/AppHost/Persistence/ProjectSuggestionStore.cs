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

        var filteredSuggestions = await FilterByHistoricalRejectionsAsync(suggestions, cancellationToken);

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

        return items
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

        var candidatePaths = suggestions
            .Select(item => item.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var historicalDecisions = await _db.ProjectSuggestions
            .AsNoTracking()
            .Where(item =>
                item.Status != ProjectSuggestionStatus.Pending
                && candidatePaths.Contains(item.Path))
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

    private static string BuildDecisionKey(string path, string kind, string fingerprint)
    {
        return $"{path.Trim().ToLowerInvariant()}::{kind.Trim().ToLowerInvariant()}::{fingerprint.Trim().ToLowerInvariant()}";
    }
}
