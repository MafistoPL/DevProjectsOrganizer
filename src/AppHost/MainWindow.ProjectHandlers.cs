using AppHost.Persistence;
using AppHost.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

    private async Task HandleProjectsRunTagHeuristicsAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!TryGetProjectId(request.Payload, out var projectId))
        {
            SendError(request.Id, request.Type, "Missing project id.");
            return;
        }

        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId);
        if (project == null)
        {
            SendError(request.Id, request.Type, "Project not found.");
            return;
        }

        SendResponse(request.Id, request.Type, new
        {
            projectId = project.Id,
            action = "TagHeuristicsQueued",
            queuedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task HandleProjectsRunAiTagSuggestionsAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!TryGetProjectId(request.Payload, out var projectId))
        {
            SendError(request.Id, request.Type, "Missing project id.");
            return;
        }

        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId);
        if (project == null)
        {
            SendError(request.Id, request.Type, "Project not found.");
            return;
        }

        SendResponse(request.Id, request.Type, new
        {
            projectId = project.Id,
            action = "AiTagSuggestionsQueued",
            queuedAt = DateTimeOffset.UtcNow
        });
    }

    private static bool TryGetProjectId(JsonElement? payload, out Guid projectId)
    {
        projectId = Guid.Empty;
        if (!payload.HasValue)
        {
            return false;
        }

        var element = payload.Value;
        if (!element.TryGetProperty("projectId", out var idElement))
        {
            return false;
        }

        return Guid.TryParse(idElement.GetString(), out projectId);
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
