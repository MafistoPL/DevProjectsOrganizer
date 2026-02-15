using AppHost.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class TagStoreTests
{
    [Fact]
    public async Task AddAsync_then_ListAllAsync_returns_tags_sorted_by_name()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var store = new TagStore(db);
            await store.AddAsync("web");
            await store.AddAsync("csharp");

            var tags = await store.ListAllAsync();

            tags.Select(tag => tag.Name).Should().Equal("csharp", "web");
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task AddAsync_throws_for_duplicate_name_case_insensitive()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var store = new TagStore(db);
            await store.AddAsync("Cpp");

            var act = () => store.AddAsync("cpp");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Tag already exists.");
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task UpdateAsync_changes_name_and_validates_duplicates()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var store = new TagStore(db);
            var first = await store.AddAsync("node");
            var second = await store.AddAsync("typescript");

            var updated = await store.UpdateAsync(first.Id, "backend");
            updated.Name.Should().Be("backend");

            var duplicateAct = () => store.UpdateAsync(second.Id, "BACKEND");
            await duplicateAct.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Tag already exists.");
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task DeleteAsync_removes_tag()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var store = new TagStore(db);
            var tag = await store.AddAsync("archive");

            var deleted = await store.DeleteAsync(tag.Id);

            deleted.Should().BeTrue();
            await using var checkDb = new AppDbContext(options);
            var rows = await checkDb.Tags.ToListAsync();
            rows.Should().BeEmpty();
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task DeleteAsync_throws_for_system_tag()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var store = new TagStore(db);
            await store.SeedDefaultTagsAsync();
            var systemTag = (await store.ListAllAsync())
                .First(tag => tag.Name == "csharp");

            var act = () => store.DeleteAsync(systemTag.Id);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("System tag cannot be deleted.");
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }
}
