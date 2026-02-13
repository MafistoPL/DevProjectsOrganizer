using System.Text.Json;
using AppHost.Persistence;
using AppHost.Services;

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
        var result = items.Select(MapTagDto).ToList();
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
            SendResponse(request.Id, request.Type, MapTagDto(added));
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
            SendResponse(request.Id, request.Type, MapTagDto(updated));
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

    private static TagDto MapTagDto(TagEntity entity)
    {
        return new TagDto(entity.Id, entity.Name, entity.CreatedAt, entity.UpdatedAt);
    }
}
