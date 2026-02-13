using AppHost.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectStoreTests
{
    [Fact]
    public async Task ListAllAsync_smoke_returns_projects_without_sqlite_datetimeoffset_orderby_crash()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var store = new ProjectStore(db);
            var first = await AddSuggestionAsync(
                db,
                scanSessionId: Guid.NewGuid(),
                name: "alpha",
                path: @"D:\code\alpha");
            await store.UpsertFromSuggestionAsync(first);

            var second = await AddSuggestionAsync(
                db,
                scanSessionId: Guid.NewGuid(),
                name: "beta",
                path: @"D:\code\beta");
            await store.UpsertFromSuggestionAsync(second);

            var list = await store.ListAllAsync();

            list.Should().HaveCount(2);
            list.Select(item => item.Name).Should().Contain(new[] { "alpha", "beta" });
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task UpsertFromSuggestionAsync_creates_project_for_new_key()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var suggestion = await AddSuggestionAsync(db, scanSessionId: Guid.NewGuid(), name: "2Dsource");
            var store = new ProjectStore(db);

            var project = await store.UpsertFromSuggestionAsync(suggestion);

            project.Name.Should().Be("2Dsource");
            project.Path.Should().Be(@"D:\code\2Dsource");

            await using var checkDb = new AppDbContext(options);
            var all = await checkDb.Projects.ToListAsync();
            all.Should().ContainSingle();
            all[0].ProjectKey.Should().Be(ProjectStore.BuildProjectKey(@"D:\code\2Dsource", "ProjectRoot"));
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task UpsertFromSuggestionAsync_updates_existing_project_with_same_key()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var first = await AddSuggestionAsync(db, Guid.NewGuid(), "2Dsource", score: 0.61);
            var store = new ProjectStore(db);
            var created = await store.UpsertFromSuggestionAsync(first);

            var second = await AddSuggestionAsync(
                db,
                Guid.NewGuid(),
                "2Dsource-renamed",
                score: 0.88,
                reason: "markers: .sln");

            var updated = await store.UpsertFromSuggestionAsync(second);

            updated.Id.Should().Be(created.Id);
            updated.Name.Should().Be("2Dsource-renamed");
            updated.Score.Should().Be(0.88);

            await using var checkDb = new AppDbContext(options);
            var all = await checkDb.Projects.ToListAsync();
            all.Should().ContainSingle();
            all[0].Name.Should().Be("2Dsource-renamed");
            all[0].Score.Should().Be(0.88);
            all[0].SourceSuggestionId.Should().Be(second.Id);
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    private static async Task<ProjectSuggestionEntity> AddSuggestionAsync(
        AppDbContext db,
        Guid scanSessionId,
        string name,
        string path = @"D:\code\2Dsource",
        double score = 0.7,
        string reason = "markers: .vcxproj")
    {
        var scanSession = new ScanSessionEntity
        {
            Id = scanSessionId,
            RootPath = @"D:\code",
            Mode = "roots",
            State = "Completed",
            DiskKey = "D:",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ScanSessions.Add(scanSession);

        var suggestion = new ProjectSuggestionEntity
        {
            Id = Guid.NewGuid(),
            ScanSessionId = scanSessionId,
            RootPath = @"D:\code",
            Name = name,
            Path = path,
            Kind = "ProjectRoot",
            Score = score,
            Reason = reason,
            ExtensionsSummary = "cpp=4",
            Fingerprint = Guid.NewGuid().ToString("N"),
            MarkersJson = "[\".vcxproj\"]",
            TechHintsJson = "[\"cpp\"]",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectSuggestionStatus.Accepted
        };
        db.ProjectSuggestions.Add(suggestion);
        await db.SaveChangesAsync();
        return suggestion;
    }
}
