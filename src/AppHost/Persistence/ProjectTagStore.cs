using Microsoft.EntityFrameworkCore;

namespace AppHost.Persistence;

public sealed class ProjectTagStore
{
    private readonly AppDbContext _db;

    public ProjectTagStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> AttachAsync(Guid projectId, Guid tagId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.ProjectTags
            .AsNoTracking()
            .AnyAsync(item => item.ProjectId == projectId && item.TagId == tagId, cancellationToken);
        if (exists)
        {
            return false;
        }

        _db.ProjectTags.Add(new ProjectTagEntity
        {
            ProjectId = projectId,
            TagId = tagId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DetachAsync(Guid projectId, Guid tagId, CancellationToken cancellationToken = default)
    {
        var link = await _db.ProjectTags
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.TagId == tagId, cancellationToken);
        if (link == null)
        {
            return false;
        }

        _db.ProjectTags.Remove(link);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
