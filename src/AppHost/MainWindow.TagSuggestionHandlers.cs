using System.Text.Json;
using AppHost.Persistence;
using AppHost.Services;
using Microsoft.EntityFrameworkCore;

namespace AppHost;

public partial class MainWindow
{
    private async Task HandleTagSuggestionsListAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        var store = new TagSuggestionStore(_dbContext);
        var items = await store.ListAllAsync();
        var result = await MapTagSuggestionDtosAsync(items);
        SendResponse(request.Id, request.Type, result);
    }

    private async Task HandleTagSuggestionsSetStatusAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!TryGetTagSuggestionId(request.Payload, out var suggestionId))
        {
            SendError(request.Id, request.Type, "Missing tag suggestion id.");
            return;
        }

        if (!TryGetTagSuggestionStatus(request.Payload, out var status))
        {
            SendError(request.Id, request.Type, "Missing or invalid tag suggestion status.");
            return;
        }

        try
        {
            var store = new TagSuggestionStore(_dbContext);
            var updated = await store.SetStatusAsync(suggestionId, status);
            var projectTagsChanged = false;
            if (status == TagSuggestionStatus.Accepted && updated.TagId.HasValue)
            {
                var projectTagStore = new ProjectTagStore(_dbContext);
                projectTagsChanged = await projectTagStore.AttachAsync(updated.ProjectId, updated.TagId.Value);
            }

            var mapped = await MapTagSuggestionDtosAsync([updated]);
            SendEvent("tagSuggestions.changed", new { id = updated.Id, status = updated.Status.ToString() });
            if (projectTagsChanged && updated.TagId.HasValue)
            {
                SendEvent("projects.changed", new
                {
                    reason = "project.tagAttached",
                    projectId = updated.ProjectId,
                    tagId = updated.TagId.Value
                });
            }
            SendResponse(request.Id, request.Type, mapped[0]);
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task<IReadOnlyList<TagSuggestionDto>> MapTagSuggestionDtosAsync(IReadOnlyList<TagSuggestionEntity> entities)
    {
        if (_dbContext == null || entities.Count == 0)
        {
            return [];
        }

        var projectIds = entities
            .Select(item => item.ProjectId)
            .Distinct()
            .ToList();
        var tagIds = entities
            .Where(item => item.TagId.HasValue)
            .Select(item => item.TagId!.Value)
            .Distinct()
            .ToList();

        var projectNames = await _dbContext.Projects
            .AsNoTracking()
            .Where(item => projectIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name);

        var tagNames = await _dbContext.Tags
            .AsNoTracking()
            .Where(item => tagIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name);

        return entities
            .Select(item =>
            {
                var projectName = projectNames.GetValueOrDefault(item.ProjectId, "(unknown project)");
                var tagName = item.TagId.HasValue
                    ? tagNames.GetValueOrDefault(item.TagId.Value, item.SuggestedTagName)
                    : item.SuggestedTagName;

                return new TagSuggestionDto(
                    item.Id,
                    item.ProjectId,
                    projectName,
                    item.TagId,
                    tagName,
                    item.Type.ToString(),
                    item.Source.ToString(),
                    item.Confidence,
                    item.Reason,
                    item.CreatedAt,
                    item.Status.ToString());
            })
            .ToList();
    }

    private static bool TryGetTagSuggestionId(JsonElement? payload, out Guid suggestionId)
    {
        suggestionId = Guid.Empty;
        if (!payload.HasValue)
        {
            return false;
        }

        var element = payload.Value;
        if (!element.TryGetProperty("id", out var idElement))
        {
            return false;
        }

        return Guid.TryParse(idElement.GetString(), out suggestionId);
    }

    private static bool TryGetTagSuggestionStatus(JsonElement? payload, out TagSuggestionStatus status)
    {
        status = TagSuggestionStatus.Pending;
        if (!payload.HasValue)
        {
            return false;
        }

        var element = payload.Value;
        if (!element.TryGetProperty("status", out var statusElement))
        {
            return false;
        }

        var raw = statusElement.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return Enum.TryParse<TagSuggestionStatus>(raw, true, out status);
    }
}
