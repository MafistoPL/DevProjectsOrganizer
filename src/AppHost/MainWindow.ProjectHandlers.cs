using AppHost.Persistence;
using AppHost.Services;
using AppHost.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.IO;

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
        var projectIds = items.Select(item => item.Id).ToList();
        var tagRows = await (
            from projectTag in _dbContext.ProjectTags.AsNoTracking()
            join tag in _dbContext.Tags.AsNoTracking()
                on projectTag.TagId equals tag.Id
            where projectIds.Contains(projectTag.ProjectId)
            orderby tag.Name
            select new
            {
                projectTag.ProjectId,
                TagId = tag.Id,
                TagName = tag.Name
            })
            .ToListAsync();
        var tagsByProjectId = tagRows
            .GroupBy(item => item.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProjectTagDto>)group
                    .Select(item => new ProjectTagDto(item.TagId, item.TagName))
                    .ToList());

        var result = items
            .Select(item =>
            {
                var tags = tagsByProjectId.GetValueOrDefault(item.Id, Array.Empty<ProjectTagDto>());
                return MapProjectDto(item, tags);
            })
            .ToList();
        SendResponse(request.Id, request.Type, result);
    }

    private async Task HandleProjectsDeleteAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!ProjectsDeletePayloadParser.TryParse(request.Payload, out var projectId))
        {
            SendError(request.Id, request.Type, "Missing project id.");
            return;
        }

        try
        {
            var store = new ProjectStore(_dbContext);
            var deleted = await store.DeleteAsync(projectId);
            if (deleted)
            {
                SendEvent("projects.changed", new
                {
                    reason = "project.deleted",
                    projectId
                });
                SendEvent("suggestions.changed", new
                {
                    reason = "project.deleted",
                    projectId
                });
            }

            SendResponse(request.Id, request.Type, new
            {
                id = projectId,
                deleted
            });
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleProjectsUpdateAsync(HostRequest request)
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

        if (!TryGetProjectDescription(request.Payload, out var description))
        {
            SendError(request.Id, request.Type, "Missing project description.");
            return;
        }

        try
        {
            var store = new ProjectStore(_dbContext);
            var updated = await store.UpdateDescriptionAsync(projectId, description);
            SendEvent("projects.changed", new
            {
                reason = "project.updated",
                projectId = updated.Id
            });
            SendResponse(request.Id, request.Type, new
            {
                id = updated.Id,
                updated = true,
                description = updated.Description
            });
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleProjectsAttachTagAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!ProjectTagMutationPayloadParser.TryParse(request.Payload, out var projectId, out var tagId))
        {
            SendError(request.Id, request.Type, "Missing project id or tag id.");
            return;
        }

        var projectExists = await _dbContext.Projects
            .AsNoTracking()
            .AnyAsync(item => item.Id == projectId);
        if (!projectExists)
        {
            SendError(request.Id, request.Type, "Project not found.");
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

        try
        {
            var store = new ProjectTagStore(_dbContext);
            var attached = await store.AttachAsync(projectId, tagId);
            if (attached)
            {
                SendEvent("projects.changed", new
                {
                    reason = "project.tagAttached",
                    projectId,
                    tagId
                });
            }

            SendResponse(request.Id, request.Type, new
            {
                projectId,
                tagId,
                attached
            });
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleProjectsDetachTagAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!ProjectTagMutationPayloadParser.TryParse(request.Payload, out var projectId, out var tagId))
        {
            SendError(request.Id, request.Type, "Missing project id or tag id.");
            return;
        }

        var projectExists = await _dbContext.Projects
            .AsNoTracking()
            .AnyAsync(item => item.Id == projectId);
        if (!projectExists)
        {
            SendError(request.Id, request.Type, "Project not found.");
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

        try
        {
            var store = new ProjectTagStore(_dbContext);
            var detached = await store.DetachAsync(projectId, tagId);
            if (detached)
            {
                SendEvent("projects.changed", new
                {
                    reason = "project.tagDetached",
                    projectId,
                    tagId
                });
            }

            SendResponse(request.Id, request.Type, new
            {
                projectId,
                tagId,
                detached
            });
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleProjectsRescanAsync(HostRequest request)
    {
        if (_dbContext == null)
        {
            SendError(request.Id, request.Type, "Database not ready.");
            return;
        }

        if (!ProjectsRescanPayloadParser.TryParse(request.Payload, out var projectId))
        {
            SendError(request.Id, request.Type, "Missing project id.");
            return;
        }

        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(item => item.Id == projectId);
        if (project == null)
        {
            SendError(request.Id, request.Type, "Project not found.");
            return;
        }

        try
        {
            await RescanProjectMetadataAsync(project);
            SendEvent("projects.changed", new
            {
                reason = "project.rescanned",
                projectId = project.Id,
                fileCount = project.FileCount
            });

            var heuristicsRun = await RunTagHeuristicsForProjectAsync(project);
            SendResponse(request.Id, request.Type, new
            {
                runId = heuristicsRun.RunId,
                projectId = project.Id,
                action = "ProjectRescanCompleted",
                generatedCount = heuristicsRun.GeneratedCount,
                fileCount = project.FileCount,
                regression = new
                {
                    baselineAcceptedCount = heuristicsRun.Regression.BaselineAcceptedCount,
                    baselineRejectedCount = heuristicsRun.Regression.BaselineRejectedCount,
                    acceptedMissingCount = heuristicsRun.Regression.AcceptedMissingCount,
                    rejectedMissingCount = heuristicsRun.Regression.RejectedMissingCount,
                    addedCount = heuristicsRun.Regression.AddedCount
                },
                outputPath = heuristicsRun.OutputPath,
                finishedAt = heuristicsRun.FinishedAt
            });
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
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
            .FirstOrDefaultAsync(item => item.Id == projectId);
        if (project == null)
        {
            SendError(request.Id, request.Type, "Project not found.");
            return;
        }

        try
        {
            var heuristicsRun = await RunTagHeuristicsForProjectAsync(project);
            SendResponse(request.Id, request.Type, new
            {
                runId = heuristicsRun.RunId,
                projectId = project.Id,
                action = "TagHeuristicsCompleted",
                generatedCount = heuristicsRun.GeneratedCount,
                regression = new
                {
                    baselineAcceptedCount = heuristicsRun.Regression.BaselineAcceptedCount,
                    baselineRejectedCount = heuristicsRun.Regression.BaselineRejectedCount,
                    acceptedMissingCount = heuristicsRun.Regression.AcceptedMissingCount,
                    rejectedMissingCount = heuristicsRun.Regression.RejectedMissingCount,
                    addedCount = heuristicsRun.Regression.AddedCount
                },
                outputPath = heuristicsRun.OutputPath,
                finishedAt = heuristicsRun.FinishedAt
            });
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
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

    private async Task<TagHeuristicsRunResult> RunTagHeuristicsForProjectAsync(ProjectEntity project)
    {
        if (_dbContext == null)
        {
            throw new InvalidOperationException("Database not ready.");
        }

        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            PublishTagHeuristicsProgress(runId, project, "Running", 10, "Loading tags", startedAt, null, null);
            var tags = await _dbContext.Tags
                .AsNoTracking()
                .ToListAsync();

            PublishTagHeuristicsProgress(runId, project, "Running", 40, "Detecting tag suggestions", startedAt, null, null);
            var heuristics = new TagSuggestionHeuristicsService();
            var detected = heuristics.Detect(project, tags);

            PublishTagHeuristicsProgress(
                runId,
                project,
                "Running",
                75,
                $"Persisting {detected.Count} suggestion(s)",
                startedAt,
                null,
                null);

            var store = new TagSuggestionStore(_dbContext);
            var regression = await store.AnalyzeRegressionForProjectAsync(project.Id, detected);
            var generated = await store.ReplaceForProjectAsync(project.Id, detected);

            PublishTagHeuristicsProgress(
                runId,
                project,
                "Running",
                90,
                "Saving run scan JSON",
                startedAt,
                null,
                generated);
            var finishedAt = DateTimeOffset.UtcNow;
            var outputPath = await SaveTagHeuristicsScanAsync(
                runId,
                project,
                tags.Count,
                detected,
                generated,
                startedAt,
                finishedAt);

            PublishTagHeuristicsProgress(
                runId,
                project,
                "Completed",
                100,
                $"Completed. Generated {generated} suggestion(s)",
                startedAt,
                finishedAt,
                generated);

            SendEvent("tagSuggestions.changed", new
            {
                reason = "project.tagHeuristics",
                projectId = project.Id,
                generatedCount = generated
            });

            return new TagHeuristicsRunResult(
                runId,
                generated,
                outputPath,
                finishedAt,
                regression);
        }
        catch (Exception ex)
        {
            PublishTagHeuristicsProgress(
                runId,
                project,
                "Failed",
                100,
                ex.Message,
                startedAt,
                DateTimeOffset.UtcNow,
                null);
            throw;
        }
    }

    private async Task RescanProjectMetadataAsync(ProjectEntity project)
    {
        if (_dbContext == null)
        {
            throw new InvalidOperationException("Database not ready.");
        }

        var path = project.Path?.Trim() ?? string.Empty;
        var scanSessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(path))
        {
            project.LastScanSessionId = scanSessionId;
            project.FileCount = 0;
            project.UpdatedAt = now;
            await _dbContext.SaveChangesAsync();
            return;
        }

        if (File.Exists(path))
        {
            project.LastScanSessionId = scanSessionId;
            project.FileCount = 1;
            project.UpdatedAt = now;
            await _dbContext.SaveChangesAsync();
            return;
        }

        if (!Directory.Exists(path))
        {
            project.LastScanSessionId = scanSessionId;
            project.FileCount = 0;
            project.UpdatedAt = now;
            await _dbContext.SaveChangesAsync();
            return;
        }

        var runtime = new ScanRuntime(
            scanSessionId,
            path,
            GetDiskKey(path),
            new ScanStartRequest("roots", null, null));
        runtime.SetState(ScanSessionStates.Counting);

        var snapshotBuilder = new ScanSnapshotBuilder();
        runtime.TotalFiles = await snapshotBuilder.CountFilesAsync(runtime);
        runtime.SetState(ScanSessionStates.Running);
        var snapshot = await snapshotBuilder.BuildSnapshotAsync(runtime, _ => Task.CompletedTask);

        var detectedProjects = new ProjectSuggestionHeuristicsService().Detect(snapshot);
        var matched = SelectRescanSuggestion(project, detectedProjects);
        if (matched != null)
        {
            ApplySuggestionMetadata(project, matched);
        }

        project.LastScanSessionId = scanSessionId;
        project.FileCount = runtime.TotalFiles ?? snapshot.FilesScanned;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    private static DetectedProjectSuggestion? SelectRescanSuggestion(
        ProjectEntity project,
        IReadOnlyList<DetectedProjectSuggestion> detected)
    {
        if (detected.Count == 0)
        {
            return null;
        }

        var normalizedProjectPath = NormalizePath(project.Path);
        return detected
            .FirstOrDefault(item =>
                string.Equals(NormalizePath(item.Path), normalizedProjectPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Kind, project.Kind, StringComparison.OrdinalIgnoreCase))
            ?? detected.FirstOrDefault(item =>
                string.Equals(NormalizePath(item.Path), normalizedProjectPath, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplySuggestionMetadata(ProjectEntity project, DetectedProjectSuggestion suggestion)
    {
        project.Name = suggestion.Name;
        project.Kind = suggestion.Kind;
        project.ProjectKey = ProjectStore.BuildProjectKey(project.Path, suggestion.Kind);
        project.Score = suggestion.Score;
        project.Reason = suggestion.Reason;
        project.ExtensionsSummary = suggestion.ExtensionsSummary;
        project.MarkersJson = JsonSerializer.Serialize(suggestion.Markers);
        project.TechHintsJson = JsonSerializer.Serialize(suggestion.TechHints);
    }

    private static string NormalizePath(string path)
    {
        return path
            .Trim()
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string GetDiskKey(string path)
    {
        var root = Path.GetPathRoot(path) ?? path;
        return root.TrimEnd(Path.DirectorySeparatorChar);
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

    private static bool TryGetProjectDescription(JsonElement? payload, out string description)
    {
        description = string.Empty;
        if (!payload.HasValue)
        {
            return false;
        }

        var element = payload.Value;
        if (!element.TryGetProperty("description", out var descriptionElement))
        {
            return false;
        }

        description = descriptionElement.GetString() ?? string.Empty;
        return true;
    }

    private static ProjectDto MapProjectDto(ProjectEntity entity, IReadOnlyList<ProjectTagDto> tags)
    {
        return new ProjectDto(
            entity.Id,
            entity.SourceSuggestionId,
            entity.LastScanSessionId,
            entity.RootPath,
            entity.Name,
            entity.Description,
            entity.FileCount,
            entity.Path,
            entity.Kind,
            entity.Score,
            entity.Reason,
            entity.ExtensionsSummary,
            DeserializeStringList(entity.MarkersJson),
            DeserializeStringList(entity.TechHintsJson),
            entity.CreatedAt,
            entity.UpdatedAt,
            tags);
    }

    private sealed record TagHeuristicsRunResult(
        Guid RunId,
        int GeneratedCount,
        string OutputPath,
        DateTimeOffset FinishedAt,
        TagSuggestionRegressionProjectReport Regression);

    private void PublishTagHeuristicsProgress(
        Guid runId,
        ProjectEntity project,
        string state,
        int progress,
        string message,
        DateTimeOffset startedAt,
        DateTimeOffset? finishedAt,
        int? generatedCount)
    {
        SendEvent("tagHeuristics.progress", new
        {
            runId,
            projectId = project.Id,
            projectName = project.Name,
            state,
            progress = Math.Clamp(progress, 0, 100),
            message,
            startedAt,
            finishedAt,
            generatedCount
        });
    }

    private static async Task<string> SaveTagHeuristicsScanAsync(
        Guid runId,
        ProjectEntity project,
        int availableTags,
        IReadOnlyList<DetectedTagSuggestion> detected,
        int generated,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt)
    {
        var scanSnapshot = new TagHeuristicsScanSnapshot
        {
            RunId = runId,
            ProjectId = project.Id,
            ProjectName = project.Name,
            ProjectPath = project.Path,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            AvailableTags = availableTags,
            DetectedSuggestions = detected.Count,
            GeneratedSuggestions = generated,
            Suggestions = detected.Select(item => new TagHeuristicsScanSuggestion
            {
                TagId = item.TagId,
                TagName = item.TagName,
                Type = item.Type,
                Source = item.Source,
                Confidence = item.Confidence,
                Reason = item.Reason,
                Fingerprint = item.Fingerprint,
                CreatedAt = item.CreatedAt
            }).ToList()
        };

        var writer = new TagHeuristicsScanWriter();
        return await writer.SaveAsync(scanSnapshot, runId, CancellationToken.None);
    }
}
