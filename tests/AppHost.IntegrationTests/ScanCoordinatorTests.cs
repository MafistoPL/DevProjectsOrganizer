using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ScanCoordinatorTests
{
    [Fact]
    public async Task ListActiveAsync_returns_historical_sessions_from_database()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            var newest = new ScanSessionEntity
            {
                Id = Guid.NewGuid(),
                RootPath = @"D:\code\newest",
                Mode = "roots",
                State = ScanSessionStates.Running,
                DiskKey = "D:",
                CreatedAt = now.AddMinutes(-1),
                StartedAt = now.AddMinutes(-1),
                FilesScanned = 25,
                TotalFiles = 100
            };
            var olderCompleted = new ScanSessionEntity
            {
                Id = Guid.NewGuid(),
                RootPath = @"D:\code\older",
                Mode = "roots",
                State = ScanSessionStates.Completed,
                DiskKey = "D:",
                CreatedAt = now.AddMinutes(-10),
                StartedAt = now.AddMinutes(-10),
                FinishedAt = now.AddMinutes(-9),
                FilesScanned = 100,
                TotalFiles = 100,
                OutputPath = @"C:\mock\scan-older.json"
            };
            var archived = new ScanSessionEntity
            {
                Id = Guid.NewGuid(),
                RootPath = @"D:\code\archived",
                Mode = "roots",
                State = ScanSessionStates.Archived,
                DiskKey = "D:",
                CreatedAt = now.AddMinutes(-20),
                StartedAt = now.AddMinutes(-20),
                FinishedAt = now.AddMinutes(-19),
                FilesScanned = 5,
                TotalFiles = 5,
                OutputPath = @"C:\mock\scan-archived.json"
            };
            db.ScanSessions.AddRange(newest, olderCompleted, archived);
            await db.SaveChangesAsync();

            var coordinator = new ScanCoordinator(() => new AppDbContext(options));
            var scans = await coordinator.ListActiveAsync(CancellationToken.None);

            scans.Should().HaveCount(3);
            scans.Select(item => item.Id).Should().ContainInOrder(newest.Id, olderCompleted.Id, archived.Id);
            scans.Single(item => item.Id == archived.Id).State.Should().Be(ScanSessionStates.Archived);
            scans.Single(item => item.Id == olderCompleted.Id).OutputPath.Should().Be(@"C:\mock\scan-older.json");
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task ArchiveCompletedAsync_moves_completed_sessions_to_archived()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            db.ScanSessions.AddRange(
                new ScanSessionEntity
                {
                    Id = Guid.NewGuid(),
                    RootPath = @"D:\code\completed-a",
                    Mode = "roots",
                    State = ScanSessionStates.Completed,
                    DiskKey = "D:",
                    CreatedAt = now.AddMinutes(-3),
                    StartedAt = now.AddMinutes(-3),
                    FinishedAt = now.AddMinutes(-2)
                },
                new ScanSessionEntity
                {
                    Id = Guid.NewGuid(),
                    RootPath = @"D:\code\completed-b",
                    Mode = "roots",
                    State = ScanSessionStates.Completed,
                    DiskKey = "D:",
                    CreatedAt = now.AddMinutes(-2),
                    StartedAt = now.AddMinutes(-2),
                    FinishedAt = now.AddMinutes(-1)
                },
                new ScanSessionEntity
                {
                    Id = Guid.NewGuid(),
                    RootPath = @"D:\code\running",
                    Mode = "roots",
                    State = ScanSessionStates.Running,
                    DiskKey = "D:",
                    CreatedAt = now.AddMinutes(-1),
                    StartedAt = now.AddMinutes(-1)
                });
            await db.SaveChangesAsync();

            var coordinator = new ScanCoordinator(() => new AppDbContext(options));
            var archivedCount = await coordinator.ArchiveCompletedAsync(CancellationToken.None);

            archivedCount.Should().Be(2);

            await using var checkDb = new AppDbContext(options);
            var states = await checkDb.ScanSessions
                .OrderBy(item => item.RootPath)
                .Select(item => new { item.RootPath, item.State })
                .ToListAsync();
            states.Should().Contain(item => item.RootPath == @"D:\code\completed-a" && item.State == ScanSessionStates.Archived);
            states.Should().Contain(item => item.RootPath == @"D:\code\completed-b" && item.State == ScanSessionStates.Archived);
            states.Should().Contain(item => item.RootPath == @"D:\code\running" && item.State == ScanSessionStates.Running);
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }

    [Fact]
    public async Task StartAsync_creates_snapshot_json_for_root_scan()
    {
        var fixtureRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DevProjectsOrganizer",
            "test-fixtures",
            $"scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureRoot);
        var testDataDir = Path.Combine(fixtureRoot, "dpo-data");

        var sourceDir = Path.Combine(fixtureRoot, "src");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "Program.cs"), "line1\nline2\nline3\n");
        await File.WriteAllTextAsync(Path.Combine(fixtureRoot, "Makefile"), "all:\n\techo ok\n");
        await File.WriteAllTextAsync(Path.Combine(fixtureRoot, "ignore-me.pdb"), "debug");

        var binDir = Path.Combine(fixtureRoot, "bin");
        Directory.CreateDirectory(binDir);
        await File.WriteAllTextAsync(Path.Combine(binDir, "ignore-me.obj"), "artifact");

        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var store = new RootStore(db);
            var root = await store.AddAsync(fixtureRoot);

            var executionService = new ScanExecutionService(
                snapshotWriter: new ScanSnapshotWriter(testDataDir));
            var coordinator = new ScanCoordinator(() => new AppDbContext(options), executionService);
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.ScanEvent += (type, data) =>
            {
                if (type == "scan.completed")
                {
                    completed.TrySetResult(true);
                }
            };

            await coordinator.StartAsync(new ScanStartRequest("roots", root.Id, 1), CancellationToken.None);

            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

            await using var checkDb = new AppDbContext(options);
            var session = await checkDb.ScanSessions.FirstOrDefaultAsync();
            session.Should().NotBeNull();
            session!.OutputPath.Should().NotBeNullOrWhiteSpace();

            File.Exists(session.OutputPath!).Should().BeTrue();

            var json = await File.ReadAllTextAsync(session.OutputPath!);
            json.Should().Contain("Program.cs");
            json.Should().Contain("Makefile");
            json.Should().NotContain("ignore-me.pdb");
            json.Should().NotContain("ignore-me.obj");

            session.TotalFiles.Should().Be(2);
            session.FilesScanned.Should().Be(2);

            var suggestions = await checkDb.ProjectSuggestions
                .Where(item => item.ScanSessionId == session.Id)
                .ToListAsync();
            suggestions.Should().NotBeEmpty();

            var makefileSuggestion = suggestions
                .Single(item => string.Equals(item.Path, fixtureRoot, StringComparison.OrdinalIgnoreCase));
            makefileSuggestion.Reason.Should().Contain("Makefile");
            makefileSuggestion.Kind.Should().Be("ProjectRoot");
            makefileSuggestion.Score.Should().BeGreaterThan(0.5);
            makefileSuggestion.Status.Should().Be(ProjectSuggestionStatus.Pending);
            makefileSuggestion.RootPath.Should().Be(fixtureRoot);

            var markers = JsonSerializer.Deserialize<string[]>(makefileSuggestion.MarkersJson);
            markers.Should().Contain("Makefile");
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, true);
            }
        }
    }
}
