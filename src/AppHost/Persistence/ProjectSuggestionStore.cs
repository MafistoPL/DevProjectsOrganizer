using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace AppHost.Persistence;

public sealed class ProjectSuggestionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;

    public ProjectSuggestionStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task ReplaceForScanAsync(
        Guid scanSessionId,
        IReadOnlyList<Services.DetectedProjectSuggestion> suggestions,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ScanSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == scanSessionId, cancellationToken);
        if (session == null)
        {
            throw new InvalidOperationException("Scan session not found.");
        }

        var existing = await _db.ProjectSuggestions
            .Where(item => item.ScanSessionId == scanSessionId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            _db.ProjectSuggestions.RemoveRange(existing);
        }

        var entities = suggestions.Select(item => new ProjectSuggestionEntity
        {
            Id = Guid.NewGuid(),
            ScanSessionId = scanSessionId,
            RootPath = session.RootPath,
            Name = item.Name,
            Path = item.Path,
            Kind = item.Kind,
            Score = item.Score,
            Reason = item.Reason,
            ExtensionsSummary = item.ExtensionsSummary,
            MarkersJson = JsonSerializer.Serialize(item.Markers, JsonOptions),
            TechHintsJson = JsonSerializer.Serialize(item.TechHints, JsonOptions),
            CreatedAt = item.CreatedAt,
            Status = ProjectSuggestionStatus.Pending
        });

        _db.ProjectSuggestions.AddRange(entities);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectSuggestionEntity>> GetByScanAsync(
        Guid scanSessionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.ProjectSuggestions
            .Where(item => item.ScanSessionId == scanSessionId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectSuggestionEntity> SetStatusAsync(
        Guid suggestionId,
        ProjectSuggestionStatus status,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ProjectSuggestions
            .FirstOrDefaultAsync(item => item.Id == suggestionId, cancellationToken);
        if (entity == null)
        {
            throw new InvalidOperationException("Project suggestion not found.");
        }

        entity.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
