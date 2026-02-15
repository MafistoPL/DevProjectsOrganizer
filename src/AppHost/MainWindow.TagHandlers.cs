using System.Text.Json;
using AppHost.Persistence;
using AppHost.Services;
using Microsoft.EntityFrameworkCore;

namespace AppHost;

public partial class MainWindow
{
    private async Task HandleTagsListAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        var store = new TagStore(_dbContext);
        var items = await store.ListAllAsync();
        var projectCounts = await _dbContext.ProjectTags
            .AsNoTracking()
            .GroupBy(item => item.TagId)
            .Select(group => new
            {
                TagId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.TagId, item => item.Count);

        var result = items
            .Select(item => MapTagDto(item, projectCounts.GetValueOrDefault(item.Id, 0)))
            .ToList();
        SendResponse(request.Id, request.Type, result);
    }

    private async Task HandleTagsProjectsAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!TryGetTagId(request.Payload, out var tagId))
        {
            SendError(request.Id, request.Type, "Missing tag id.");
            return;
        }

        var tagExists = await _dbContext.Tags
            .AsNoTracking()
            .AnyAsync(item => item.Id == tagId);
        if (!tagExists)
        {
            SendError(request.Id, request.Type, "Tag not found.");
            return;
        }

        var store = new ProjectStore(_dbContext);
        var items = await store.ListByTagAsync(tagId);
        var result = items
            .Select(item => new TagLinkedProjectDto(
                item.Id,
                item.Name,
                item.Path,
                item.Kind,
                item.UpdatedAt))
            .ToList();

        SendResponse(request.Id, request.Type, result);
    }

    private async Task HandleTagsAddAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!TryGetTagName(request.Payload, out var name))
        {
            SendError(request.Id, request.Type, "Missing tag name.");
            return;
        }

        try
        {
            var store = new TagStore(_dbContext);
            var added = await store.AddAsync(name);
            SendResponse(request.Id, request.Type, MapTagDto(added, 0));
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleTagsUpdateAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!TryGetTagId(request.Payload, out var id))
        {
            SendError(request.Id, request.Type, "Missing tag id.");
            return;
        }

        if (!TryGetTagName(request.Payload, out var name))
        {
            SendError(request.Id, request.Type, "Missing tag name.");
            return;
        }

        try
        {
            var store = new TagStore(_dbContext);
            var updated = await store.UpdateAsync(id, name);
            var projectCount = await _dbContext.ProjectTags
                .AsNoTracking()
                .CountAsync(item => item.TagId == id);
            SendResponse(request.Id, request.Type, MapTagDto(updated, projectCount));
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleTagsDeleteAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!TryGetTagId(request.Payload, out var id))
        {
            SendError(request.Id, request.Type, "Missing tag id.");
            return;
        }

        try
        {
            var store = new TagStore(_dbContext);
            var deleted = await store.DeleteAsync(id);
            SendResponse(request.Id, request.Type, new { id, deleted });
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private static bool TryGetTagId(JsonElement? payload, out Guid id)
    {
        id = Guid.Empty;
        if (!payload.HasValue)
        {
            return false;
        }

        var element = payload.Value;
        if (!element.TryGetProperty("id", out var idElement))
        {
            return false;
        }

        return Guid.TryParse(idElement.GetString(), out id);
    }

    private static bool TryGetTagName(JsonElement? payload, out string name)
    {
        name = string.Empty;
        if (!payload.HasValue)
        {
            return false;
        }

        var element = payload.Value;
        if (!element.TryGetProperty("name", out var nameElement))
        {
            return false;
        }

        var raw = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        name = raw;
        return true;
    }

    private static TagDto MapTagDto(TagEntity entity, int projectCount)
    {
        return new TagDto(
            entity.Id,
            entity.Name,
            entity.IsSystem,
            projectCount,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
