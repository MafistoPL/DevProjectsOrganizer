using Microsoft.EntityFrameworkCore;
using System.Linq;

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
    private static readonly HashSet<string> DefaultTagNormalizedSet = DefaultTags
        .Select(tag => tag.Trim().ToLowerInvariant())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        var existing = await _db.Tags.ToListAsync(cancellationToken);
        var byNormalized = existing.ToDictionary(tag => tag.NormalizedName, StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        var added = 0;
        foreach (var name in DefaultTags)
        {
            var (raw, normalized) = NormalizeName(name);
            if (byNormalized.TryGetValue(normalized, out var found))
            {
                if (!found.IsSystem)
                {
                    found.IsSystem = true;
                    found.UpdatedAt = now;
                }

                continue;
            }

            _db.Tags.Add(new TagEntity
            {
                Id = Guid.NewGuid(),
                Name = raw,
                NormalizedName = normalized,
                IsSystem = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            added++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return added;
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
            IsSystem = false,
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

        if (entity.IsSystem || DefaultTagNormalizedSet.Contains(entity.NormalizedName))
        {
            throw new InvalidOperationException("System tag cannot be deleted.");
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
