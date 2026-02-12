using System;
using AppHost.Services;

namespace AppHost;

public partial class MainWindow
{
    private async Task HandleRootsListAsync(HostRequest request)
    {
        if (_rootStore == null)
        {
            SendError(request.Id, request.Type, "Root store not ready.");
            return;
        }

        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database context not ready.");
            return;
        }

        var roots = await _rootStore.GetAllAsync();
        var overviewService = new RootOverviewService(_dbContext);
        var overview = await overviewService.BuildAsync(roots, CancellationToken.None);
        SendResponse(request.Id, request.Type, overview);
    }

    private async Task HandleRootsAddAsync(HostRequest request)
    {
        if (_rootStore == null)
        {
            SendError(request.Id, request.Type, "Root store not ready.");
            return;
        }

        if (!request.Payload.HasValue
            || !request.Payload.Value.TryGetProperty("path", out var pathElement))
        {
            SendError(request.Id, request.Type, "Missing root path.");
            return;
        }

        var path = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(path))
        {
            SendError(request.Id, request.Type, "Root path cannot be empty.");
            return;
        }

        var root = await _rootStore.AddAsync(path);
        SendResponse(request.Id, request.Type, root);
    }

    private async Task HandleRootsUpdateAsync(HostRequest request)
    {
        if (_rootStore == null)
        {
            SendError(request.Id, request.Type, "Root store not ready.");
            return;
        }

        if (!request.Payload.HasValue)
        {
            SendError(request.Id, request.Type, "Missing payload.");
            return;
        }

        var payload = request.Payload.Value;
        if (!payload.TryGetProperty("id", out var idElement))
        {
            SendError(request.Id, request.Type, "Missing root id.");
            return;
        }

        if (!payload.TryGetProperty("path", out var pathElement))
        {
            SendError(request.Id, request.Type, "Missing root path.");
            return;
        }

        var idValue = idElement.GetString();
        if (!Guid.TryParse(idValue, out var rootId))
        {
            SendError(request.Id, request.Type, "Invalid root id.");
            return;
        }

        var path = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(path))
        {
            SendError(request.Id, request.Type, "Root path cannot be empty.");
            return;
        }

        try
        {
            var root = await _rootStore.UpdateAsync(rootId, path);
            SendResponse(request.Id, request.Type, root);
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleRootsDeleteAsync(HostRequest request)
    {
        if (_rootStore == null)
        {
            SendError(request.Id, request.Type, "Root store not ready.");
            return;
        }

        if (!request.Payload.HasValue)
        {
            SendError(request.Id, request.Type, "Missing payload.");
            return;
        }

        var payload = request.Payload.Value;
        if (!payload.TryGetProperty("id", out var idElement))
        {
            SendError(request.Id, request.Type, "Missing root id.");
            return;
        }

        var idValue = idElement.GetString();
        if (!Guid.TryParse(idValue, out var rootId))
        {
            SendError(request.Id, request.Type, "Invalid root id.");
            return;
        }

        var deleted = await _rootStore.DeleteAsync(rootId);
        SendResponse(request.Id, request.Type, new { id = rootId, deleted });
    }
}
