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

    [Fact]
    public async Task ReplaceForProjectAsync_skips_already_attached_or_historically_accepted()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var (project, tag) = await SeedProjectAndTagAsync(db, "cpp");
            var now = DateTimeOffset.UtcNow;

            db.ProjectTags.Add(new ProjectTagEntity
            {
                ProjectId = project.Id,
                TagId = tag.Id,
                CreatedAt = now.AddMinutes(-5)
            });

            db.TagSuggestions.Add(new TagSuggestionEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TagId = tag.Id,
                SuggestedTagName = tag.Name,
                Type = TagSuggestionType.AssignExisting,
                Source = TagSuggestionSource.Heuristic,
                Confidence = 0.9,
                Reason = "marker:.vcxproj",
                Fingerprint = "fp-accepted",
                CreatedAt = now.AddMinutes(-4),
                Status = TagSuggestionStatus.Accepted
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
                        0.82,
                        "hint:cpp",
                        "fp-new",
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
    public async Task DeleteRejectedAsync_allows_only_rejected_items()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var (project, tag) = await SeedProjectAndTagAsync(db, "native");
            var rejected = new TagSuggestionEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TagId = tag.Id,
                SuggestedTagName = tag.Name,
                Type = TagSuggestionType.AssignExisting,
                Source = TagSuggestionSource.Heuristic,
                Confidence = 0.75,
                Reason = "marker:.vcxproj",
                Fingerprint = "fp-rejected-delete",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                Status = TagSuggestionStatus.Rejected
            };
            var pending = new TagSuggestionEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TagId = tag.Id,
                SuggestedTagName = tag.Name,
                Type = TagSuggestionType.AssignExisting,
                Source = TagSuggestionSource.Heuristic,
                Confidence = 0.78,
                Reason = "hint:cpp",
                Fingerprint = "fp-pending-delete",
                CreatedAt = DateTimeOffset.UtcNow,
                Status = TagSuggestionStatus.Pending
            };
            db.TagSuggestions.AddRange(rejected, pending);
            await db.SaveChangesAsync();

            var store = new TagSuggestionStore(db);
            var deleted = await store.DeleteRejectedAsync(rejected.Id);
            deleted.Should().BeTrue();

            var deletePending = async () => await store.DeleteRejectedAsync(pending.Id);
            await deletePending.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Only rejected tag suggestions can be deleted.");

            await using var checkDb = new AppDbContext(options);
            (await checkDb.TagSuggestions.AnyAsync(item => item.Id == rejected.Id)).Should().BeFalse();
            (await checkDb.TagSuggestions.AnyAsync(item => item.Id == pending.Id)).Should().BeTrue();
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task AnalyzeRegressionForProjectAsync_counts_missing_and_added_against_latest_decisions()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var (project, cppTag) = await SeedProjectAndTagAsync(db, "cpp");
            var nativeTag = new TagEntity
            {
                Id = Guid.NewGuid(),
                Name = "native",
                NormalizedName = "native",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Tags.Add(nativeTag);
            await db.SaveChangesAsync();

            var now = DateTimeOffset.UtcNow;
            db.TagSuggestions.AddRange(
                new TagSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    TagId = cppTag.Id,
                    SuggestedTagName = cppTag.Name,
                    Type = TagSuggestionType.AssignExisting,
                    Source = TagSuggestionSource.Heuristic,
                    Confidence = 0.86,
                    Reason = "accepted baseline",
                    Fingerprint = "fp-a1",
                    CreatedAt = now.AddMinutes(-3),
                    Status = TagSuggestionStatus.Accepted
                },
                new TagSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    TagId = nativeTag.Id,
                    SuggestedTagName = nativeTag.Name,
                    Type = TagSuggestionType.AssignExisting,
                    Source = TagSuggestionSource.Heuristic,
                    Confidence = 0.66,
                    Reason = "rejected baseline",
                    Fingerprint = "fp-r1",
                    CreatedAt = now.AddMinutes(-2),
                    Status = TagSuggestionStatus.Rejected
                });
            await db.SaveChangesAsync();

            var store = new TagSuggestionStore(db);
            var report = await store.AnalyzeRegressionForProjectAsync(project.Id,
                [
                    new DetectedTagSuggestion(
                        cppTag.Id,
                        cppTag.Name,
                        TagSuggestionType.AssignExisting.ToString(),
                        TagSuggestionSource.Heuristic.ToString(),
                        0.9,
                        "still detected",
                        "fp-a2",
                        now),
                    new DetectedTagSuggestion(
                        null,
                        "hello-world",
                        TagSuggestionType.AssignExisting.ToString(),
                        TagSuggestionSource.Heuristic.ToString(),
                        0.71,
                        "new detected",
                        "fp-new",
                        now)
                ]);

            report.BaselineAcceptedCount.Should().Be(1);
            report.BaselineRejectedCount.Should().Be(1);
            report.AcceptedMissingCount.Should().Be(0);
            report.RejectedMissingCount.Should().Be(1);
            report.AddedCount.Should().Be(1);
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task ReplaceForProjectAsync_with_ai_scope_replaces_only_pending_ai_rows()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var (project, tag) = await SeedProjectAndTagAsync(db, "aleksandra-site");
            var now = DateTimeOffset.UtcNow;

            db.TagSuggestions.AddRange(
                new TagSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    TagId = tag.Id,
                    SuggestedTagName = tag.Name,
                    Type = TagSuggestionType.AssignExisting,
                    Source = TagSuggestionSource.Heuristic,
                    Confidence = 0.8,
                    Reason = "heuristic baseline",
                    Fingerprint = "fp-heur-old",
                    CreatedAt = now.AddMinutes(-2),
                    Status = TagSuggestionStatus.Pending
                },
                new TagSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    TagId = tag.Id,
                    SuggestedTagName = tag.Name,
                    Type = TagSuggestionType.AssignExisting,
                    Source = TagSuggestionSource.Ai,
                    Confidence = 0.7,
                    Reason = "ai baseline",
                    Fingerprint = "fp-ai-old",
                    CreatedAt = now.AddMinutes(-1),
                    Status = TagSuggestionStatus.Pending
                });
            await db.SaveChangesAsync();

            var store = new TagSuggestionStore(db);
            var inserted = await store.ReplaceForProjectAsync(
                project.Id,
                [
                    new DetectedTagSuggestion(
                        tag.Id,
                        tag.Name,
                        TagSuggestionType.AssignExisting.ToString(),
                        TagSuggestionSource.Ai.ToString(),
                        0.93,
                        "ai:metadata phrase",
                        "fp-ai-new",
                        now)
                ],
                TagSuggestionSource.Ai);

            inserted.Should().Be(1);

            await using var checkDb = new AppDbContext(options);
            var rows = await checkDb.TagSuggestions
                .AsNoTracking()
                .Where(item => item.ProjectId == project.Id && item.Status == TagSuggestionStatus.Pending)
                .OrderBy(item => item.Source)
                .ToListAsync();

            rows.Should().HaveCount(2);
            rows.Should().Contain(item => item.Source == TagSuggestionSource.Heuristic && item.Fingerprint == "fp-heur-old");
            rows.Should().Contain(item => item.Source == TagSuggestionSource.Ai && item.Fingerprint == "fp-ai-new");
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
