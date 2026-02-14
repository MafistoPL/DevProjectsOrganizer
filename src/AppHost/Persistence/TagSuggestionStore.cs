using AppHost.Services;
using Microsoft.EntityFrameworkCore;

namespace AppHost.Persistence;

public sealed class TagSuggestionStore
{
    private readonly AppDbContext _db;

    public TagSuggestionStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TagSuggestionEntity>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _db.TagSuggestions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
    }

    public async Task<TagSuggestionEntity> SetStatusAsync(
        Guid id,
        TagSuggestionStatus status,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.TagSuggestions
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity == null)
        {
            throw new InvalidOperationException("Tag suggestion not found.");
        }

        entity.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<int> ReplaceForProjectAsync(
        Guid projectId,
        IReadOnlyList<DetectedTagSuggestion> suggestions,
        CancellationToken cancellationToken = default)
    {
        var existingPending = await _db.TagSuggestions
            .Where(item =>
                item.ProjectId == projectId
                && item.Status == TagSuggestionStatus.Pending
                && item.Source == TagSuggestionSource.Heuristic)
            .ToListAsync(cancellationToken);
        if (existingPending.Count > 0)
        {
            _db.TagSuggestions.RemoveRange(existingPending);
        }

        var deduped = suggestions
            .GroupBy(item => BuildDecisionKey(
                projectId,
                item.TagId,
                item.TagName,
                item.Type,
                item.Fingerprint), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.Confidence)
                .First())
            .ToList();

        var filtered = await FilterByHistoricalRejectionsAsync(projectId, deduped, cancellationToken);

        var entities = filtered.Select(item => new TagSuggestionEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TagId = item.TagId,
            SuggestedTagName = item.TagName,
            Type = ParseType(item.Type),
            Source = ParseSource(item.Source),
            Confidence = item.Confidence,
            Reason = item.Reason,
            Fingerprint = item.Fingerprint,
            CreatedAt = item.CreatedAt,
            Status = TagSuggestionStatus.Pending
        }).ToList();

        if (entities.Count > 0)
        {
            _db.TagSuggestions.AddRange(entities);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return entities.Count;
    }

    private async Task<IReadOnlyList<DetectedTagSuggestion>> FilterByHistoricalRejectionsAsync(
        Guid projectId,
        IReadOnlyList<DetectedTagSuggestion> suggestions,
        CancellationToken cancellationToken)
    {
        if (suggestions.Count == 0)
        {
            return suggestions;
        }

        var allHistory = await _db.TagSuggestions
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Status != TagSuggestionStatus.Pending)
            .ToListAsync(cancellationToken);

        var latestByDecisionKey = allHistory
            .GroupBy(item => BuildDecisionKey(
                item.ProjectId,
                item.TagId,
                item.SuggestedTagName,
                item.Type.ToString(),
                item.Fingerprint), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.CreatedAt).First())
            .ToDictionary(
                item => BuildDecisionKey(
                    item.ProjectId,
                    item.TagId,
                    item.SuggestedTagName,
                    item.Type.ToString(),
                    item.Fingerprint),
                item => item.Status,
                StringComparer.OrdinalIgnoreCase);

        return suggestions
            .Where(item =>
            {
                var key = BuildDecisionKey(projectId, item.TagId, item.TagName, item.Type, item.Fingerprint);
                return !latestByDecisionKey.TryGetValue(key, out var status) || status != TagSuggestionStatus.Rejected;
            })
            .ToList();
    }

    private static string BuildDecisionKey(
        Guid projectId,
        Guid? tagId,
        string tagName,
        string type,
        string fingerprint)
    {
        var tagPart = tagId.HasValue
            ? tagId.Value.ToString("D")
            : tagName.Trim().ToLowerInvariant();

        return $"{projectId:D}::{type.Trim().ToLowerInvariant()}::{tagPart}::{fingerprint.Trim().ToLowerInvariant()}";
    }

    private static TagSuggestionType ParseType(string raw)
    {
        return Enum.TryParse<TagSuggestionType>(raw, true, out var value)
            ? value
            : TagSuggestionType.AssignExisting;
    }

    private static TagSuggestionSource ParseSource(string raw)
    {
        return Enum.TryParse<TagSuggestionSource>(raw, true, out var value)
            ? value
            : TagSuggestionSource.Heuristic;
    }
}
