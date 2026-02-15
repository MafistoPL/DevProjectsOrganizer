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

    public async Task<bool> DeleteRejectedAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.TagSuggestions
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        if (entity.Status != TagSuggestionStatus.Rejected)
        {
            throw new InvalidOperationException("Only rejected tag suggestions can be deleted.");
        }

        _db.TagSuggestions.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TagSuggestionRegressionProjectReport> AnalyzeRegressionForProjectAsync(
        Guid projectId,
        IReadOnlyList<DetectedTagSuggestion> detected,
        CancellationToken cancellationToken = default)
    {
        var baselineHistory = await _db.TagSuggestions
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Status != TagSuggestionStatus.Pending)
            .ToListAsync(cancellationToken);

        var latestByDecisionKey = baselineHistory
            .GroupBy(
                item => BuildRegressionDecisionKey(item.ProjectId, item.TagId, item.SuggestedTagName, item.Type.ToString()),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.CreatedAt)
                .First())
            .ToList();

        var recomputedKeys = detected
            .Select(item => BuildRegressionDecisionKey(projectId, item.TagId, item.TagName, item.Type))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var acceptedMissingCount = latestByDecisionKey.Count(item =>
            item.Status == TagSuggestionStatus.Accepted
            && !recomputedKeys.Contains(BuildRegressionDecisionKey(
                item.ProjectId,
                item.TagId,
                item.SuggestedTagName,
                item.Type.ToString())));

        var rejectedMissingCount = latestByDecisionKey.Count(item =>
            item.Status == TagSuggestionStatus.Rejected
            && !recomputedKeys.Contains(BuildRegressionDecisionKey(
                item.ProjectId,
                item.TagId,
                item.SuggestedTagName,
                item.Type.ToString())));

        var baselineKeySet = latestByDecisionKey
            .Select(item => BuildRegressionDecisionKey(item.ProjectId, item.TagId, item.SuggestedTagName, item.Type.ToString()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addedCount = recomputedKeys.Count(key => !baselineKeySet.Contains(key));

        return new TagSuggestionRegressionProjectReport(
            BaselineAcceptedCount: latestByDecisionKey.Count(item => item.Status == TagSuggestionStatus.Accepted),
            BaselineRejectedCount: latestByDecisionKey.Count(item => item.Status == TagSuggestionStatus.Rejected),
            AcceptedMissingCount: acceptedMissingCount,
            RejectedMissingCount: rejectedMissingCount,
            AddedCount: addedCount);
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
        filtered = await FilterAlreadyAcceptedOrAttachedAsync(projectId, filtered, cancellationToken);

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

    private async Task<IReadOnlyList<DetectedTagSuggestion>> FilterAlreadyAcceptedOrAttachedAsync(
        Guid projectId,
        IReadOnlyList<DetectedTagSuggestion> suggestions,
        CancellationToken cancellationToken)
    {
        if (suggestions.Count == 0)
        {
            return suggestions;
        }

        var candidateTagIds = suggestions
            .Where(item => item.TagId.HasValue)
            .Select(item => item.TagId!.Value)
            .Distinct()
            .ToList();
        var attachedTagIds = candidateTagIds.Count == 0
            ? new HashSet<Guid>()
            : await _db.ProjectTags
                .AsNoTracking()
                .Where(item => item.ProjectId == projectId && candidateTagIds.Contains(item.TagId))
                .Select(item => item.TagId)
                .ToHashSetAsync(cancellationToken);

        var historicalAccepted = await _db.TagSuggestions
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Status == TagSuggestionStatus.Accepted)
            .ToListAsync(cancellationToken);

        var latestAcceptedByDecision = historicalAccepted
            .GroupBy(
                item => BuildRegressionDecisionKey(item.ProjectId, item.TagId, item.SuggestedTagName, item.Type.ToString()),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.CreatedAt)
                .First())
            .Select(item => BuildRegressionDecisionKey(item.ProjectId, item.TagId, item.SuggestedTagName, item.Type.ToString()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return suggestions
            .Where(item =>
            {
                if (item.TagId.HasValue && attachedTagIds.Contains(item.TagId.Value))
                {
                    return false;
                }

                var decisionKey = BuildRegressionDecisionKey(projectId, item.TagId, item.TagName, item.Type);
                return !latestAcceptedByDecision.Contains(decisionKey);
            })
            .ToList();
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

    private static string BuildRegressionDecisionKey(
        Guid projectId,
        Guid? tagId,
        string tagName,
        string type)
    {
        var tagPart = tagId.HasValue
            ? tagId.Value.ToString("D")
            : tagName.Trim().ToLowerInvariant();

        return $"{projectId:D}::{type.Trim().ToLowerInvariant()}::{tagPart}";
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
