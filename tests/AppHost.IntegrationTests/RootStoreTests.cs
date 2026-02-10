using AppHost.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class RootStoreTests
{
    [Fact]
    public async Task Migrate_creates_roots_table()
    {
        var (options, db, path) = await CreateDbAsync();
        try
        {
            await using var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='roots';";
            var result = await command.ExecuteScalarAsync();

            result.Should().Be("roots");
        }
        finally
        {
            await DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task AddAsync_then_GetAllAsync_returns_roots()
    {
        var (options, db, path) = await CreateDbAsync();
        try
        {
            var store = new RootStore(db);

            await store.AddAsync(@"D:\code");
            await store.AddAsync(@"C:\src");

            var roots = await store.GetAllAsync();

            roots.Should().HaveCount(2);
            roots.Select(r => r.Path).Should().BeEquivalentTo(new[] { @"C:\src", @"D:\code" }, options => options.WithStrictOrdering());
        }
        finally
        {
            await DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task UpdateAsync_changes_path()
    {
        var (options, db, path) = await CreateDbAsync();
        try
        {
            var store = new RootStore(db);

            var root = await store.AddAsync(@"D:\code");
            var updated = await store.UpdateAsync(root.Id, @"D:\code-new");

            updated.Path.Should().Be(@"D:\code-new");

            var roots = await store.GetAllAsync();
            roots.Should().ContainSingle(r => r.Path == @"D:\code-new");
        }
        finally
        {
            await DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task DeleteAsync_removes_root()
    {
        var (options, db, path) = await CreateDbAsync();
        try
        {
            var store = new RootStore(db);

            var root = await store.AddAsync(@"D:\code");
            var deleted = await store.DeleteAsync(root.Id);

            deleted.Should().BeTrue();

            var roots = await store.GetAllAsync();
            roots.Should().BeEmpty();
        }
        finally
        {
            await DisposeDbAsync(db, path);
        }
    }

    internal static async Task<(DbContextOptions<AppDbContext> Options, AppDbContext Db, string Path)> CreateDbAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"dpo-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(
                $"Data Source={dbPath};Pooling=False",
                sqlite => sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        return (options, db, dbPath);
    }

    internal static async Task DisposeDbAsync(AppDbContext db, string path)
    {
        await db.DisposeAsync();

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
