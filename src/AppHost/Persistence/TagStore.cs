using Microsoft.EntityFrameworkCore;

namespace AppHost.Persistence;

public sealed class TagStore
{
    private readonly AppDbContext _db;

    public TagStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TagEntity>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Tags
            .AsNoTracking()
            .OrderBy(tag => tag.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<TagEntity> AddAsync(string name, CancellationToken cancellationToken = default)
    {
        var (raw, normalized) = NormalizeName(name);

        var duplicate = await _db.Tags
            .AsNoTracking()
            .AnyAsync(tag => tag.NormalizedName == normalized, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("Tag already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new TagEntity
        {
            Id = Guid.NewGuid(),
            Name = raw,
            NormalizedName = normalized,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Tags.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<TagEntity> UpdateAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        var (raw, normalized) = NormalizeName(name);

        var entity = await _db.Tags
            .FirstOrDefaultAsync(tag => tag.Id == id, cancellationToken);
        if (entity == null)
        {
            throw new InvalidOperationException("Tag not found.");
        }

        var duplicate = await _db.Tags
            .AsNoTracking()
            .AnyAsync(tag => tag.NormalizedName == normalized && tag.Id != id, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("Tag already exists.");
        }

        entity.Name = raw;
        entity.NormalizedName = normalized;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Tags
            .FirstOrDefaultAsync(tag => tag.Id == id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        _db.Tags.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static (string Raw, string Normalized) NormalizeName(string name)
    {
        var raw = name.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("Tag name cannot be empty.", nameof(name));
        }

        var normalized = raw.ToLowerInvariant();
        return (raw, normalized);
    }
}
