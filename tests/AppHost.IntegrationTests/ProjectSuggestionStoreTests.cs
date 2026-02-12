using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectSuggestionStoreTests
{
    [Fact]
    public async Task ListAllAsync_returns_suggestions_sorted_by_created_at_desc()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var scanId = Guid.NewGuid();
            db.ScanSessions.Add(new ScanSessionEntity
            {
                Id = scanId,
                RootPath = @"D:\code",
                Mode = "roots",
                State = ScanSessionStates.Completed,
                DiskKey = "D:",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            });

            db.ProjectSuggestions.AddRange(
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = scanId,
                    RootPath = @"D:\code",
                    Name = "older",
                    Path = @"D:\code\older",
                    Kind = "ProjectRoot",
                    Score = 0.5,
                    Reason = "markers: Makefile",
                    ExtensionsSummary = "c=2",
                    MarkersJson = "[\"Makefile\"]",
                    TechHintsJson = "[\"native\"]",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                    Status = ProjectSuggestionStatus.Pending
                },
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = scanId,
                    RootPath = @"D:\code",
                    Name = "newer",
                    Path = @"D:\code\newer",
                    Kind = "ProjectRoot",
                    Score = 0.8,
                    Reason = "markers: .sln",
                    ExtensionsSummary = "cs=5",
                    MarkersJson = "[\".sln\"]",
                    TechHintsJson = "[\"csharp\"]",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Status = ProjectSuggestionStatus.Pending
                });

            await db.SaveChangesAsync();

            var store = new ProjectSuggestionStore(new AppDbContext(options));
            var list = await store.ListAllAsync();

            list.Should().HaveCount(2);
            list[0].Name.Should().Be("newer");
            list[1].Name.Should().Be("older");
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task SetStatusAsync_updates_entity_status()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var scanId = Guid.NewGuid();
            db.ScanSessions.Add(new ScanSessionEntity
            {
                Id = scanId,
                RootPath = @"D:\code",
                Mode = "roots",
                State = ScanSessionStates.Completed,
                DiskKey = "D:",
                CreatedAt = DateTimeOffset.UtcNow
            });

            var suggestion = new ProjectSuggestionEntity
            {
                Id = Guid.NewGuid(),
                ScanSessionId = scanId,
                RootPath = @"D:\code",
                Name = "candidate",
                Path = @"D:\code\candidate",
                Kind = "ProjectRoot",
                Score = 0.62,
                Reason = "markers: package.json",
                ExtensionsSummary = "ts=3",
                MarkersJson = "[\"package.json\"]",
                TechHintsJson = "[\"typescript\"]",
                CreatedAt = DateTimeOffset.UtcNow,
                Status = ProjectSuggestionStatus.Pending
            };
            db.ProjectSuggestions.Add(suggestion);
            await db.SaveChangesAsync();

            var store = new ProjectSuggestionStore(new AppDbContext(options));
            var updated = await store.SetStatusAsync(suggestion.Id, ProjectSuggestionStatus.Accepted);

            updated.Status.Should().Be(ProjectSuggestionStatus.Accepted);

            await using var checkDb = new AppDbContext(options);
            var stored = await checkDb.ProjectSuggestions.FindAsync(suggestion.Id);
            stored.Should().NotBeNull();
            stored!.Status.Should().Be(ProjectSuggestionStatus.Accepted);
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task ReplaceForScanAsync_skips_suggestion_when_latest_matching_decision_is_rejected()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var rootPath = @"D:\code";
            var previousScanId = Guid.NewGuid();
            var currentScanId = Guid.NewGuid();

            db.ScanSessions.AddRange(
                new ScanSessionEntity
                {
                    Id = previousScanId,
                    RootPath = rootPath,
                    Mode = "roots",
                    State = ScanSessionStates.Completed,
                    DiskKey = "D:",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                },
                new ScanSessionEntity
                {
                    Id = currentScanId,
                    RootPath = rootPath,
                    Mode = "roots",
                    State = ScanSessionStates.Completed,
                    DiskKey = "D:",
                    CreatedAt = DateTimeOffset.UtcNow
                });

            db.ProjectSuggestions.Add(new ProjectSuggestionEntity
            {
                Id = Guid.NewGuid(),
                ScanSessionId = previousScanId,
                RootPath = rootPath,
                Name = "2Dsource",
                Path = @"D:\code\2Dsource",
                Kind = "ProjectRoot",
                Score = 0.7,
                Reason = "markers: .vcxproj",
                ExtensionsSummary = "cpp=4",
                Fingerprint = "fp-2dsource-v1",
                MarkersJson = "[\".vcxproj\"]",
                TechHintsJson = "[\"cpp\"]",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
                Status = ProjectSuggestionStatus.Rejected
            });

            await db.SaveChangesAsync();

            var store = new ProjectSuggestionStore(new AppDbContext(options));
            await store.ReplaceForScanAsync(
                currentScanId,
                new[]
                {
                    CreateDetectedSuggestion(
                        name: "2Dsource",
                        path: @"D:\code\2Dsource",
                        kind: "ProjectRoot",
                        fingerprint: "fp-2dsource-v1")
                });

            await using var checkDb = new AppDbContext(options);
            var currentScanSuggestions = await checkDb.ProjectSuggestions
                .Where(item => item.ScanSessionId == currentScanId)
                .ToListAsync();

            currentScanSuggestions.Should().BeEmpty();
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task DeleteAsync_removes_archived_rejection_and_allows_reinsertion_on_next_scan()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var rootPath = @"D:\code";
            var previousScanId = Guid.NewGuid();
            var currentScanId = Guid.NewGuid();

            db.ScanSessions.AddRange(
                new ScanSessionEntity
                {
                    Id = previousScanId,
                    RootPath = rootPath,
                    Mode = "roots",
                    State = ScanSessionStates.Completed,
                    DiskKey = "D:",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                },
                new ScanSessionEntity
                {
                    Id = currentScanId,
                    RootPath = rootPath,
                    Mode = "roots",
                    State = ScanSessionStates.Completed,
                    DiskKey = "D:",
                    CreatedAt = DateTimeOffset.UtcNow
                });

            var archivedRejection = new ProjectSuggestionEntity
            {
                Id = Guid.NewGuid(),
                ScanSessionId = previousScanId,
                RootPath = rootPath,
                Name = "2Dsource",
                Path = @"D:\code\2Dsource",
                Kind = "ProjectRoot",
                Score = 0.7,
                Reason = "markers: .vcxproj",
                ExtensionsSummary = "cpp=4",
                Fingerprint = "fp-2dsource-v1",
                MarkersJson = "[\".vcxproj\"]",
                TechHintsJson = "[\"cpp\"]",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
                Status = ProjectSuggestionStatus.Rejected
            };
            db.ProjectSuggestions.Add(archivedRejection);

            await db.SaveChangesAsync();

            var store = new ProjectSuggestionStore(new AppDbContext(options));
            var deleted = await store.DeleteAsync(archivedRejection.Id);
            deleted.Should().BeTrue();

            await store.ReplaceForScanAsync(
                currentScanId,
                new[]
                {
                    CreateDetectedSuggestion(
                        name: "2Dsource",
                        path: @"D:\code\2Dsource",
                        kind: "ProjectRoot",
                        fingerprint: "fp-2dsource-v1")
                });

            await using var checkDb = new AppDbContext(options);
            var currentScanSuggestions = await checkDb.ProjectSuggestions
                .Where(item => item.ScanSessionId == currentScanId)
                .ToListAsync();

            currentScanSuggestions.Should().ContainSingle();
            currentScanSuggestions[0].Path.Should().Be(@"D:\code\2Dsource");
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    private static DetectedProjectSuggestion CreateDetectedSuggestion(
        string name,
        string path,
        string kind,
        string fingerprint)
    {
        return new DetectedProjectSuggestion(
            name,
            path,
            kind,
            0.7,
            "markers: .vcxproj",
            "cpp=4",
            new[] { ".vcxproj" },
            new[] { "cpp" },
            fingerprint,
            DateTimeOffset.UtcNow);
    }
}
