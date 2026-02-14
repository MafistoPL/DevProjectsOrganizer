using Microsoft.EntityFrameworkCore;

namespace AppHost.Persistence;

public sealed class TagStore
{
    private static readonly string[] DefaultTags =
    [
        "csharp",
        "dotnet",
        "cpp",
        "c",
        "native",
        "vs-solution",
        "vs-project",
        "node",
        "react",
        "angular",
        "html",
        "json",
        "git",
        "cmake",
        "makefile",
        "java",
        "gradle",
        "maven",
        "python",
        "rust",
        "go",
        "powershell",
        "low-level",
        "console",
        "winapi",
        "gui"
    ];

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

    public async Task<int> SeedDefaultTagsAsync(CancellationToken cancellationToken = default)
    {
        var existingCount = await _db.Tags.CountAsync(cancellationToken);
        if (existingCount > 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var name in DefaultTags)
        {
            var (raw, normalized) = NormalizeName(name);
            _db.Tags.Add(new TagEntity
            {
                Id = Guid.NewGuid(),
                Name = raw,
                NormalizedName = normalized,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return DefaultTags.Length;
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
