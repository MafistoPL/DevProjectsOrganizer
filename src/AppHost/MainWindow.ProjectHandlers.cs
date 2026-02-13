using AppHost.Persistence;
using AppHost.Services;

namespace AppHost;

public partial class MainWindow
{
    private async Task HandleProjectsListAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        var store = new ProjectStore(_dbContext);
        var items = await store.ListAllAsync();
        var result = items.Select(MapProjectDto).ToList();
        SendResponse(request.Id, request.Type, result);
    }

    private static ProjectDto MapProjectDto(ProjectEntity entity)
    {
        return new ProjectDto(
            entity.Id,
            entity.SourceSuggestionId,
            entity.LastScanSessionId,
            entity.RootPath,
            entity.Name,
            entity.Path,
            entity.Kind,
            entity.Score,
            entity.Reason,
            entity.ExtensionsSummary,
            DeserializeStringList(entity.MarkersJson),
            DeserializeStringList(entity.TechHintsJson),
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
