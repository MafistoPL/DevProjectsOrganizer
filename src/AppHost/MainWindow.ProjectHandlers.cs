using AppHost.Persistence;
using AppHost.Services;
using AppHost.Contracts;
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

            SendResponse(request.Id, request.Type, new
            {
                runId,
                projectId = project.Id,
                action = "TagHeuristicsCompleted",
                generatedCount = generated,
                regression = new
                {
                    baselineAcceptedCount = regression.BaselineAcceptedCount,
                    baselineRejectedCount = regression.BaselineRejectedCount,
                    acceptedMissingCount = regression.AcceptedMissingCount,
                    rejectedMissingCount = regression.RejectedMissingCount,
                    addedCount = regression.AddedCount
                },
                outputPath,
                finishedAt
            });
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

    private static ProjectDto MapProjectDto(ProjectEntity entity, IReadOnlyList<ProjectTagDto> tags)
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
            entity.UpdatedAt,
            tags);
    }

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
