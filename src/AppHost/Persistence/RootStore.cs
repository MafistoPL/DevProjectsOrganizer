using Engine.Abstractions;
using Engine.Models;
using Microsoft.EntityFrameworkCore;

namespace AppHost.Persistence;

public sealed class RootStore : IRootStore
{
    private readonly AppDbContext _db;

    public RootStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Root>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Roots
            .AsNoTracking()
            .OrderBy(root => root.Path)
            .Select(root => new Root(root.Id, root.Path, root.Status, root.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Root> AddAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = path.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Root path cannot be empty.", nameof(path));
        }

        var existing = await _db.Roots
            .FirstOrDefaultAsync(root => root.Path == normalized, cancellationToken);
        if (existing != null)
        {
            return new Root(existing.Id, existing.Path, existing.Status, existing.CreatedAt);
        }

        var entity = new RootEntity
        {
            Id = Guid.NewGuid(),
            Path = normalized,
            Status = "not scanned",
            CreatedAt = DateTime.UtcNow
        };

        _db.Roots.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return new Root(entity.Id, entity.Path, entity.Status, entity.CreatedAt);
    }

    public async Task<Root> UpdateAsync(Guid id, string path, CancellationToken cancellationToken = default)
    {
        var normalized = path.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Root path cannot be empty.", nameof(path));
        }

        var entity = await _db.Roots.FirstOrDefaultAsync(root => root.Id == id, cancellationToken);
        if (entity == null)
        {
            throw new InvalidOperationException("Root not found.");
        }

        var duplicate = await _db.Roots.AnyAsync(
            root => root.Path == normalized && root.Id != id,
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("Root path already exists.");
        }

        entity.Path = normalized;
        await _db.SaveChangesAsync(cancellationToken);

        return new Root(entity.Id, entity.Path, entity.Status, entity.CreatedAt);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Roots.FirstOrDefaultAsync(root => root.Id == id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        _db.Roots.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SeedDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var hasAny = await _db.Roots.AnyAsync(cancellationToken);
        if (hasAny)
        {
            return;
        }

        _db.Roots.AddRange(
            new RootEntity
            {
                Id = Guid.NewGuid(),
                Path = @"D:\code",
                Status = "scanned",
                CreatedAt = DateTime.UtcNow
            },
            new RootEntity
            {
                Id = Guid.NewGuid(),
                Path = @"C:\src",
                Status = "changed",
                CreatedAt = DateTime.UtcNow
            },
            new RootEntity
            {
                Id = Guid.NewGuid(),
                Path = @"E:\backup",
                Status = "scanning",
                CreatedAt = DateTime.UtcNow
            }
        );

        await _db.SaveChangesAsync(cancellationToken);
    }
}
