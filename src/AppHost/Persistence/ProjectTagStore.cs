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
}
