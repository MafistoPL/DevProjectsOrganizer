using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class TagSuggestionStoreTests
{
    [Fact]
    public async Task ReplaceForProjectAsync_replaces_pending_and_deduplicates_by_decision_key()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var (project, tag) = await SeedProjectAndTagAsync(db, "cpp");
            var store = new TagSuggestionStore(db);
            var now = DateTimeOffset.UtcNow;

            var inserted = await store.ReplaceForProjectAsync(project.Id,
                [
                    new DetectedTagSuggestion(
                        tag.Id,
                        "cpp",
                        TagSuggestionType.AssignExisting.ToString(),
                        TagSuggestionSource.Heuristic.ToString(),
                        0.81,
                        "marker:.vcxproj",
                        "fp-1",
                        now),
                    new DetectedTagSuggestion(
                        tag.Id,
                        "cpp",
                        TagSuggestionType.AssignExisting.ToString(),
                        TagSuggestionSource.Heuristic.ToString(),
                        0.72,
                        "hint:cpp",
                        "fp-1",
                        now)
                ]);

            inserted.Should().Be(1);

            var firstPass = await store.ListAllAsync();
            firstPass.Should().ContainSingle();
            firstPass.Single().Confidence.Should().Be(0.81);

            var secondInserted = await store.ReplaceForProjectAsync(project.Id,
                [
                    new DetectedTagSuggestion(
                        tag.Id,
                        "cpp",
                        TagSuggestionType.AssignExisting.ToString(),
                        TagSuggestionSource.Heuristic.ToString(),
                        0.9,
                        "ext:cpp=10",
                        "fp-2",
                        now.AddMinutes(1))
                ]);

            secondInserted.Should().Be(1);

            await using var checkDb = new AppDbContext(options);
            var rows = (await checkDb.TagSuggestions
                    .AsNoTracking()
                    .Where(item => item.ProjectId == project.Id)
                    .ToListAsync())
                .OrderByDescending(item => item.CreatedAt)
                .ToList();

            rows.Should().ContainSingle();
            rows[0].Fingerprint.Should().Be("fp-2");
            rows[0].Status.Should().Be(TagSuggestionStatus.Pending);
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task ReplaceForProjectAsync_skips_suggestion_rejected_with_same_fingerprint()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var (project, tag) = await SeedProjectAndTagAsync(db, "native");
            var now = DateTimeOffset.UtcNow;
            db.TagSuggestions.Add(new TagSuggestionEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TagId = tag.Id,
                SuggestedTagName = tag.Name,
                Type = TagSuggestionType.AssignExisting,
                Source = TagSuggestionSource.Heuristic,
                Confidence = 0.75,
                Reason = "marker:.vcxproj",
                Fingerprint = "fp-rejected",
                CreatedAt = now.AddMinutes(-1),
                Status = TagSuggestionStatus.Rejected
            });
            await db.SaveChangesAsync();

            var store = new TagSuggestionStore(db);
            var inserted = await store.ReplaceForProjectAsync(project.Id,
                [
                    new DetectedTagSuggestion(
                        tag.Id,
                        tag.Name,
                        TagSuggestionType.AssignExisting.ToString(),
                        TagSuggestionSource.Heuristic.ToString(),
                        0.84,
                        "marker:.vcxproj",
                        "fp-rejected",
                        now)
                ]);

            inserted.Should().Be(0);

            await using var checkDb = new AppDbContext(options);
            var pending = await checkDb.TagSuggestions
                .AsNoTracking()
                .Where(item => item.ProjectId == project.Id && item.Status == TagSuggestionStatus.Pending)
                .ToListAsync();
            pending.Should().BeEmpty();
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task SetStatusAsync_updates_row_status()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var (project, tag) = await SeedProjectAndTagAsync(db, "dotnet");
            var entity = new TagSuggestionEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TagId = tag.Id,
                SuggestedTagName = tag.Name,
                Type = TagSuggestionType.AssignExisting,
                Source = TagSuggestionSource.Heuristic,
                Confidence = 0.82,
                Reason = "marker:.csproj",
                Fingerprint = "fp-status",
                CreatedAt = DateTimeOffset.UtcNow,
                Status = TagSuggestionStatus.Pending
            };
            db.TagSuggestions.Add(entity);
            await db.SaveChangesAsync();

            var store = new TagSuggestionStore(db);
            var updated = await store.SetStatusAsync(entity.Id, TagSuggestionStatus.Accepted);

            updated.Status.Should().Be(TagSuggestionStatus.Accepted);
            await using var checkDb = new AppDbContext(options);
            var fromDb = await checkDb.TagSuggestions.AsNoTracking().SingleAsync(item => item.Id == entity.Id);
            fromDb.Status.Should().Be(TagSuggestionStatus.Accepted);
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    private static async Task<(ProjectEntity Project, TagEntity Tag)> SeedProjectAndTagAsync(AppDbContext db, string tagName)
    {
        var now = DateTimeOffset.UtcNow;
        var tag = new TagEntity
        {
            Id = Guid.NewGuid(),
            Name = tagName,
            NormalizedName = tagName.ToLowerInvariant(),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Tags.Add(tag);

        var projectPath = $@"D:\code\{tagName}-project";
        var project = new ProjectEntity
        {
            Id = Guid.NewGuid(),
            SourceSuggestionId = Guid.NewGuid(),
            LastScanSessionId = Guid.NewGuid(),
            RootPath = @"D:\code",
            Name = $"{tagName}-project",
            Path = projectPath,
            Kind = "ProjectRoot",
            ProjectKey = ProjectStore.BuildProjectKey(projectPath, "ProjectRoot"),
            Score = 0.8,
            Reason = "seed",
            ExtensionsSummary = "cpp=2",
            MarkersJson = "[]",
            TechHintsJson = "[]",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Projects.Add(project);

        await db.SaveChangesAsync();
        return (project, tag);
    }
}
