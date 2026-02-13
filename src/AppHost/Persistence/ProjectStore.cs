using Microsoft.EntityFrameworkCore;

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
        return await _db.Projects
            .AsNoTracking()
            .OrderByDescending(project => project.UpdatedAt)
            .ThenBy(project => project.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectEntity> UpsertFromSuggestionAsync(
        ProjectSuggestionEntity suggestion,
        CancellationToken cancellationToken = default)
    {
        var key = BuildProjectKey(suggestion.Path, suggestion.Kind);
        var now = DateTimeOffset.UtcNow;

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
        existing.Score = suggestion.Score;
        existing.Reason = suggestion.Reason;
        existing.ExtensionsSummary = suggestion.ExtensionsSummary;
        existing.MarkersJson = suggestion.MarkersJson;
        existing.TechHintsJson = suggestion.TechHintsJson;
        existing.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public static string BuildProjectKey(string path, string kind)
    {
        return $"{kind.Trim().ToLowerInvariant()}::{path.Trim().ToLowerInvariant()}";
    }
}
