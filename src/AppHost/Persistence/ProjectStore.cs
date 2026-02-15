using Microsoft.EntityFrameworkCore;
using Engine.Scanning;
using System.IO;

namespace AppHost.Persistence;

public sealed class ProjectStore
{
    private readonly AppDbContext _db;

    public ProjectStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProjectEntity>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _db.Projects
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(project => project.UpdatedAt)
            .ThenBy(project => project.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<ProjectEntity>> ListByTagAsync(
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        var items = await (
            from project in _db.Projects.AsNoTracking()
            join projectTag in _db.ProjectTags.AsNoTracking()
                on project.Id equals projectTag.ProjectId
            where projectTag.TagId == tagId
            select project)
            .ToListAsync(cancellationToken);

        return items
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(project => project.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ProjectEntity> UpsertFromSuggestionAsync(
        ProjectSuggestionEntity suggestion,
        string? acceptedDescription = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildProjectKey(suggestion.Path, suggestion.Kind);
        var now = DateTimeOffset.UtcNow;
        var fileCount = CountProjectFiles(suggestion.Path);

        var existing = await _db.Projects
            .FirstOrDefaultAsync(project => project.ProjectKey == key, cancellationToken);

        if (existing == null)
        {
            var created = new ProjectEntity
            {
                Id = Guid.NewGuid(),
                SourceSuggestionId = suggestion.Id,
                LastScanSessionId = suggestion.ScanSessionId,
                RootPath = suggestion.RootPath,
                Name = suggestion.Name,
                Path = suggestion.Path,
                Kind = suggestion.Kind,
                ProjectKey = key,
                Description = NormalizeDescription(acceptedDescription) ?? string.Empty,
                FileCount = fileCount,
                Score = suggestion.Score,
                Reason = suggestion.Reason,
                ExtensionsSummary = suggestion.ExtensionsSummary,
                MarkersJson = suggestion.MarkersJson,
                TechHintsJson = suggestion.TechHintsJson,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Projects.Add(created);
            await _db.SaveChangesAsync(cancellationToken);
            return created;
        }

        existing.SourceSuggestionId = suggestion.Id;
        existing.LastScanSessionId = suggestion.ScanSessionId;
        existing.RootPath = suggestion.RootPath;
        existing.Name = suggestion.Name;
        existing.Path = suggestion.Path;
        existing.Kind = suggestion.Kind;
        existing.ProjectKey = key;
        var normalizedDescription = NormalizeDescription(acceptedDescription);
        if (normalizedDescription is not null)
        {
            existing.Description = normalizedDescription;
        }

        existing.FileCount = fileCount;
        existing.Score = suggestion.Score;
        existing.Reason = suggestion.Reason;
        existing.ExtensionsSummary = suggestion.ExtensionsSummary;
        existing.MarkersJson = suggestion.MarkersJson;
        existing.TechHintsJson = suggestion.TechHintsJson;
        existing.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects
            .FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project == null)
        {
            return false;
        }

        _db.Projects.Remove(project);

        var sourceSuggestion = await _db.ProjectSuggestions
            .FirstOrDefaultAsync(item => item.Id == project.SourceSuggestionId, cancellationToken);
        if (sourceSuggestion != null)
        {
            sourceSuggestion.Status = ProjectSuggestionStatus.Rejected;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ProjectEntity> UpdateDescriptionAsync(
        Guid projectId,
        string description,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Projects
            .FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (entity == null)
        {
            throw new InvalidOperationException("Project not found.");
        }

        entity.Description = NormalizeDescription(description) ?? string.Empty;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public static string BuildProjectKey(string path, string kind)
    {
        return $"{kind.Trim().ToLowerInvariant()}::{path.Trim().ToLowerInvariant()}";
    }

    private static string? NormalizeDescription(string? description)
    {
        if (description is null)
        {
            return null;
        }

        return description.Trim();
    }

    private static long CountProjectFiles(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return 0;
        }

        try
        {
            if (File.Exists(path))
            {
                return 1;
            }

            if (!Directory.Exists(path))
            {
                return 0;
            }
        }
        catch
        {
            return 0;
        }

        long count = 0;
        var pending = new Stack<string>();
        pending.Push(path);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(current);
            }
            catch
            {
                continue;
            }

            foreach (var entry in entries)
            {
                try
                {
                    if (Directory.Exists(entry))
                    {
                        var name = Path.GetFileName(entry.TrimEnd(Path.DirectorySeparatorChar));
                        if (ScanIgnorePolicy.ShouldSkipDirectory(name))
                        {
                            continue;
                        }

                        pending.Push(entry);
                        continue;
                    }

                    var fileName = Path.GetFileName(entry);
                    var extension = Path.GetExtension(entry);
                    if (ScanIgnorePolicy.ShouldSkipFile(fileName, extension))
                    {
                        continue;
                    }

                    count++;
                }
                catch
                {
                    // Ignore inaccessible entries and continue counting.
                }
            }
        }

        return count;
    }
}
