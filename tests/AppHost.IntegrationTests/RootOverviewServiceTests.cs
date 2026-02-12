using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class RootOverviewServiceTests
{
    [Fact]
    public async Task BuildAsync_returns_counts_and_latest_scan_summary_per_root()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var rootStore = new RootStore(db);
            var rootA = await rootStore.AddAsync(@"D:\code");
            var rootB = await rootStore.AddAsync(@"C:\src");

            db.ScanSessions.AddRange(
                new ScanSessionEntity
                {
                    Id = Guid.NewGuid(),
                    RootId = rootA.Id,
                    RootPath = rootA.Path,
                    Mode = "roots",
                    State = ScanSessionStates.Completed,
                    DiskKey = "D:",
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                    FinishedAt = DateTimeOffset.UtcNow.AddHours(-2),
                    FilesScanned = 120
                },
                new ScanSessionEntity
                {
                    Id = Guid.NewGuid(),
                    RootId = rootA.Id,
                    RootPath = rootA.Path,
                    Mode = "roots",
                    State = ScanSessionStates.Running,
                    DiskKey = "D:",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    FilesScanned = 55
                });

            db.ProjectSuggestions.AddRange(
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = Guid.NewGuid(),
                    RootPath = rootA.Path,
                    Name = "dotnet-api",
                    Path = @"D:\code\dotnet-api",
                    Kind = "ProjectRoot",
                    Score = 0.9,
                    Reason = "markers: .sln",
                    ExtensionsSummary = "cs=42",
                    MarkersJson = "[]",
                    TechHintsJson = "[]",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
                    Status = ProjectSuggestionStatus.Pending
                },
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = Guid.NewGuid(),
                    RootPath = rootA.Path,
                    Name = "archived",
                    Path = @"D:\code\archived",
                    Kind = "ProjectRoot",
                    Score = 0.6,
                    Reason = "markers: .csproj",
                    ExtensionsSummary = "cs=11",
                    MarkersJson = "[]",
                    TechHintsJson = "[]",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
                    Status = ProjectSuggestionStatus.Accepted
                });

            await db.SaveChangesAsync();

            var roots = await rootStore.GetAllAsync();
            var service = new RootOverviewService(db);
            var overview = await service.BuildAsync(roots);

            overview.Should().HaveCount(2);

            var rootAOverview = overview.Single(item => item.Path == rootA.Path);
            rootAOverview.ProjectCount.Should().Be(2);
            rootAOverview.OngoingSuggestionCount.Should().Be(1);
            rootAOverview.LastScanState.Should().Be(ScanSessionStates.Running);
            rootAOverview.LastScanFiles.Should().Be(55);
            rootAOverview.LastScanAt.Should().NotBeNull();

            var rootBOverview = overview.Single(item => item.Path == rootB.Path);
            rootBOverview.ProjectCount.Should().Be(0);
            rootBOverview.OngoingSuggestionCount.Should().Be(0);
            rootBOverview.LastScanState.Should().BeNull();
            rootBOverview.LastScanFiles.Should().BeNull();
            rootBOverview.LastScanAt.Should().BeNull();
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }
}
