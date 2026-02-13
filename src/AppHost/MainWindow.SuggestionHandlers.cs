using System.Text.Json;
using System.Diagnostics;
using System.IO;
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
            if (status == ProjectSuggestionStatus.Accepted)
            {
                var projectStore = new ProjectStore(_dbContext);
                await projectStore.UpsertFromSuggestionAsync(updated);
                SendEvent("projects.changed", new { reason = "suggestion.accepted", suggestionId = updated.Id });
            }
            SendResponse(request.Id, request.Type, MapSuggestionDto(updated));
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleSuggestionsDeleteAsync(HostRequest request)
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

        try
        {
            var store = new ProjectSuggestionStore(_dbContext);
            var deleted = await store.DeleteAsync(suggestionId);
            SendResponse(request.Id, request.Type, new { id = suggestionId, deleted });
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
                suggestion.Fingerprint,
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

    private async Task HandleSuggestionsRegressionReportAsync(HostRequest request)
    {
        try
        {
            var regression = new ProjectSuggestionRegressionService(
                () => new AppDbContext(AppDbContext.CreateDefaultOptions()));
            var report = await regression.AnalyzeReplayFromHistoryAsync(CancellationToken.None);

            SendResponse(request.Id, request.Type, report);
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleSuggestionsExportRegressionReportAsync(HostRequest request)
    {
        try
        {
            var regression = new ProjectSuggestionRegressionService(
                () => new AppDbContext(AppDbContext.CreateDefaultOptions()));
            var result = await regression.ExportReplayFromHistoryAsync(CancellationToken.None);

            SendResponse(request.Id, request.Type, new
            {
                path = result.Path,
                rootsAnalyzed = result.RootsAnalyzed
            });
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleSuggestionsExportArchiveAsync(HostRequest request)
    {
        var exporter = new ProjectSuggestionArchiveService(() => new AppDbContext(AppDbContext.CreateDefaultOptions()));
        var result = await exporter.ExportAsync(CancellationToken.None);

        SendResponse(request.Id, request.Type, new
        {
            path = result.Path,
            count = result.Count
        });
    }

    private Task HandleSuggestionsOpenArchiveFolderAsync(HostRequest request)
    {
        try
        {
            var exporter = new ProjectSuggestionArchiveService(() => new AppDbContext(AppDbContext.CreateDefaultOptions()));
            var path = exporter.EnsureArchiveDirectory();

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });

            SendResponse(request.Id, request.Type, new { path });
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }

        return Task.CompletedTask;
    }

    private Task HandleSuggestionsOpenPathAsync(HostRequest request)
    {
        try
        {
            if (!TryGetPath(request.Payload, out var path))
            {
                SendError(request.Id, request.Type, "Missing path.");
                return Task.CompletedTask;
            }

            OpenInExplorer(path);
            SendResponse(request.Id, request.Type, new { path });
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }

        return Task.CompletedTask;
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

    private static bool TryGetPath(JsonElement? payload, out string path)
    {
        path = string.Empty;
        if (!payload.HasValue)
        {
            return false;
        }

        var element = payload.Value;
        if (!element.TryGetProperty("path", out var pathElement))
        {
            return false;
        }

        var raw = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        path = raw.Trim();
        return true;
    }

    private static void OpenInExplorer(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
            return;
        }

        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
            return;
        }

        throw new InvalidOperationException("Path does not exist.");
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
