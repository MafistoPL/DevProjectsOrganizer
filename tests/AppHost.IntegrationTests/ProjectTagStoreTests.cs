using AppHost.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectTagStoreTests
{
    [Fact]
    public async Task AttachAsync_creates_link_once_and_is_idempotent()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            var tag = new TagEntity
            {
                Id = Guid.NewGuid(),
                Name = "cpp",
                NormalizedName = "cpp",
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Tags.Add(tag);

            var projectPath = @"D:\code\cpp-project";
            var project = new ProjectEntity
            {
                Id = Guid.NewGuid(),
                SourceSuggestionId = Guid.NewGuid(),
                LastScanSessionId = Guid.NewGuid(),
                RootPath = @"D:\code",
                Name = "cpp-project",
                Path = projectPath,
                Kind = "ProjectRoot",
                ProjectKey = ProjectStore.BuildProjectKey(projectPath, "ProjectRoot"),
                Score = 0.7,
                Reason = "seed",
                ExtensionsSummary = "cpp=1",
                MarkersJson = "[]",
                TechHintsJson = "[]",
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var store = new ProjectTagStore(db);

            var created = await store.AttachAsync(project.Id, tag.Id);
            var second = await store.AttachAsync(project.Id, tag.Id);

            created.Should().BeTrue();
            second.Should().BeFalse();

            await using var checkDb = new AppDbContext(options);
            var links = await checkDb.ProjectTags
                .AsNoTracking()
                .Where(item => item.ProjectId == project.Id && item.TagId == tag.Id)
                .ToListAsync();

            links.Should().ContainSingle();
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task DetachAsync_removes_existing_link_and_returns_false_when_missing()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            var tag = new TagEntity
            {
                Id = Guid.NewGuid(),
                Name = "cpp",
                NormalizedName = "cpp",
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Tags.Add(tag);

            var projectPath = @"D:\code\cpp-project";
            var project = new ProjectEntity
            {
                Id = Guid.NewGuid(),
                SourceSuggestionId = Guid.NewGuid(),
                LastScanSessionId = Guid.NewGuid(),
                RootPath = @"D:\code",
                Name = "cpp-project",
                Path = projectPath,
                Kind = "ProjectRoot",
                ProjectKey = ProjectStore.BuildProjectKey(projectPath, "ProjectRoot"),
                Score = 0.7,
                Reason = "seed",
                ExtensionsSummary = "cpp=1",
                MarkersJson = "[]",
                TechHintsJson = "[]",
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Projects.Add(project);
            db.ProjectTags.Add(new ProjectTagEntity
            {
                ProjectId = project.Id,
                TagId = tag.Id,
                CreatedAt = now
            });
            await db.SaveChangesAsync();

            var store = new ProjectTagStore(db);

            var detached = await store.DetachAsync(project.Id, tag.Id);
            var second = await store.DetachAsync(project.Id, tag.Id);

            detached.Should().BeTrue();
            second.Should().BeFalse();

            await using var checkDb = new AppDbContext(options);
            var links = await checkDb.ProjectTags
                .AsNoTracking()
                .Where(item => item.ProjectId == project.Id && item.TagId == tag.Id)
                .ToListAsync();

            links.Should().BeEmpty();
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }
}
