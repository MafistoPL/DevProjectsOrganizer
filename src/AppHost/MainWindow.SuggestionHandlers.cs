using System.Text.Json;
using AppHost.Persistence;
using AppHost.Services;

namespace AppHost;

public partial class MainWindow
{
    private async Task HandleSuggestionsListAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        var store = new ProjectSuggestionStore(_dbContext);
        var items = await store.ListAllAsync();
        var result = items.Select(MapSuggestionDto).ToList();
        SendResponse(request.Id, request.Type, result);
    }

    private async Task HandleSuggestionsSetStatusAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!TryGetSuggestionId(request.Payload, out var suggestionId))
        {
            SendError(request.Id, request.Type, "Missing suggestion id.");
            return;
        }

        if (!TryGetSuggestionStatus(request.Payload, out var status))
        {
            SendError(request.Id, request.Type, "Missing or invalid suggestion status.");
            return;
        }

        try
        {
            var store = new ProjectSuggestionStore(_dbContext);
            var updated = await store.SetStatusAsync(suggestionId, status);
            SendResponse(request.Id, request.Type, MapSuggestionDto(updated));
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleSuggestionsExportDebugAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!TryGetSuggestionId(request.Payload, out var suggestionId))
        {
            SendError(request.Id, request.Type, "Missing suggestion id.");
            return;
        }

        var store = new ProjectSuggestionStore(_dbContext);
        var suggestion = await store.GetByIdAsync(suggestionId);
        if (suggestion == null)
        {
            SendError(request.Id, request.Type, "Suggestion not found.");
            return;
        }

        var session = await _dbContext.ScanSessions.FindAsync(suggestion.ScanSessionId);
        var markers = DeserializeStringList(suggestion.MarkersJson);
        var techHints = DeserializeStringList(suggestion.TechHintsJson);

        var payload = new
        {
            suggestion = new
            {
                suggestion.Id,
                suggestion.ScanSessionId,
                suggestion.RootPath,
                suggestion.Name,
                suggestion.Path,
                suggestion.Kind,
                suggestion.Score,
                suggestion.Reason,
                suggestion.ExtensionsSummary,
                markers,
                techHints,
                status = suggestion.Status.ToString(),
                suggestion.CreatedAt
            },
            source = new
            {
                scanOutputPath = session?.OutputPath,
                scanMode = session?.Mode,
                scanState = session?.State,
                scanCreatedAt = session?.CreatedAt,
                scanFinishedAt = session?.FinishedAt
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        SendResponse(request.Id, request.Type, new
        {
            id = suggestion.Id,
            json
        });
    }

    private static bool TryGetSuggestionId(JsonElement? payload, out Guid suggestionId)
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

    private static bool TryGetSuggestionStatus(JsonElement? payload, out ProjectSuggestionStatus status)
    {
        status = ProjectSuggestionStatus.Pending;
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

        return Enum.TryParse<ProjectSuggestionStatus>(raw, true, out status);
    }

    private static ProjectSuggestionDto MapSuggestionDto(ProjectSuggestionEntity entity)
    {
        return new ProjectSuggestionDto(
            entity.Id,
            entity.ScanSessionId,
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
            entity.Status.ToString());
    }

    private static IReadOnlyList<string> DeserializeStringList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
