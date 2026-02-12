using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectSuggestionRegressionServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_reports_missing_accepted_and_rejected_from_history()
    {
        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var rootPath = @"D:\code";
            var previousScan = new ScanSessionEntity
            {
                Id = Guid.NewGuid(),
                RootPath = rootPath,
                Mode = "roots",
                State = ScanSessionStates.Completed,
                DiskKey = "D:",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                FinishedAt = DateTimeOffset.UtcNow.AddMinutes(-9)
            };
            var currentScan = new ScanSessionEntity
            {
                Id = Guid.NewGuid(),
                RootPath = rootPath,
                Mode = "roots",
                State = ScanSessionStates.Completed,
                DiskKey = "D:",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                FinishedAt = DateTimeOffset.UtcNow
            };

            db.ScanSessions.AddRange(previousScan, currentScan);
            db.ProjectSuggestions.AddRange(
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = previousScan.Id,
                    RootPath = rootPath,
                    Name = "accepted-old",
                    Path = @"D:\code\accepted-old",
                    Kind = "ProjectRoot",
                    Score = 0.9,
                    Reason = "markers: .sln",
                    ExtensionsSummary = "cs=12",
                    MarkersJson = "[\".sln\"]",
                    TechHintsJson = "[\"csharp\"]",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                    Status = ProjectSuggestionStatus.Accepted
                },
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = previousScan.Id,
                    RootPath = rootPath,
                    Name = "rejected-old",
                    Path = @"D:\code\rejected-old",
                    Kind = "ProjectRoot",
                    Score = 0.4,
                    Reason = "markers: Makefile",
                    ExtensionsSummary = "c=10",
                    MarkersJson = "[\"Makefile\"]",
                    TechHintsJson = "[\"native\"]",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-8),
                    Status = ProjectSuggestionStatus.Rejected
                },
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = currentScan.Id,
                    RootPath = rootPath,
                    Name = "accepted-old",
                    Path = @"D:\code\accepted-old",
                    Kind = "ProjectRoot",
                    Score = 0.91,
                    Reason = "markers: .sln",
                    ExtensionsSummary = "cs=15",
                    MarkersJson = "[\".sln\"]",
                    TechHintsJson = "[\"csharp\"]",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Status = ProjectSuggestionStatus.Pending
                },
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = currentScan.Id,
                    RootPath = rootPath,
                    Name = "new-project",
                    Path = @"D:\code\new-project",
                    Kind = "ProjectRoot",
                    Score = 0.72,
                    Reason = "markers: package.json",
                    ExtensionsSummary = "ts=20",
                    MarkersJson = "[\"package.json\"]",
                    TechHintsJson = "[\"typescript\"]",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Status = ProjectSuggestionStatus.Pending
                });
            await db.SaveChangesAsync();

            var sut = new ProjectSuggestionRegressionService(() => new AppDbContext(options));
            var report = await sut.AnalyzeAsync(currentScan.Id);

            report.RootPath.Should().Be(rootPath);
            report.BaselineAcceptedCount.Should().Be(1);
            report.BaselineRejectedCount.Should().Be(1);
            report.AcceptedMissingCount.Should().Be(0);
            report.RejectedMissingCount.Should().Be(1);
            report.RejectedMissingPaths.Should().Contain(@"D:\code\rejected-old");
            report.AddedCount.Should().Be(1);
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
        }
    }
}
