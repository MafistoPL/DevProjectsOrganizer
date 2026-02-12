using System.Text.Json;
using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectSuggestionArchiveServiceTests
{
    [Fact]
    public async Task ExportAsync_writes_json_with_non_pending_suggestions_only()
    {
        var (options, db, dbPath) = await RootStoreTests.CreateDbAsync();
        var exportDir = Path.Combine(Path.GetTempPath(), $"dpo-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(exportDir);

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

            db.ProjectSuggestions.AddRange(
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = scanId,
                    RootPath = @"D:\code",
                    Name = "accepted-one",
                    Path = @"D:\code\accepted-one",
                    Kind = "ProjectRoot",
                    Score = 0.8,
                    Reason = "markers: .sln",
                    ExtensionsSummary = "cs=12",
                    MarkersJson = "[\".sln\"]",
                    TechHintsJson = "[\"csharp\"]",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                    Status = ProjectSuggestionStatus.Accepted
                },
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = scanId,
                    RootPath = @"D:\code",
                    Name = "rejected-one",
                    Path = @"D:\code\rejected-one",
                    Kind = "ProjectRoot",
                    Score = 0.3,
                    Reason = "markers: Makefile",
                    ExtensionsSummary = "c=4",
                    MarkersJson = "[\"Makefile\"]",
                    TechHintsJson = "[\"native\"]",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                    Status = ProjectSuggestionStatus.Rejected
                },
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = scanId,
                    RootPath = @"D:\code",
                    Name = "pending-one",
                    Path = @"D:\code\pending-one",
                    Kind = "ProjectRoot",
                    Score = 0.6,
                    Reason = "markers: package.json",
                    ExtensionsSummary = "ts=7",
                    MarkersJson = "[\"package.json\"]",
                    TechHintsJson = "[\"typescript\"]",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Status = ProjectSuggestionStatus.Pending
                });
            await db.SaveChangesAsync();

            var service = new ProjectSuggestionArchiveService(() => new AppDbContext(options), exportDir);
            var result = await service.ExportAsync();

            result.Count.Should().Be(2);
            File.Exists(result.Path).Should().BeTrue();

            var json = await File.ReadAllTextAsync(result.Path);
            json.Should().Contain("accepted-one");
            json.Should().Contain("rejected-one");
            json.Should().NotContain("pending-one");

            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("count").GetInt32().Should().Be(2);
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, dbPath);
            if (Directory.Exists(exportDir))
            {
                Directory.Delete(exportDir, true);
            }
        }
    }
}
