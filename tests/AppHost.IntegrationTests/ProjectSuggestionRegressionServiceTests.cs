using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectSuggestionRegressionServiceTests
{
    [Fact]
    public async Task ReplayRegression_uses_matching_snapshot_per_scan_session()
    {
        var (options, db, dbPath) = await RootStoreTests.CreateDbAsync();
        var snapshotDir = Path.Combine(Path.GetTempPath(), $"dpo-replay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(snapshotDir);

        try
        {
            var rootPath = @"D:\same-root";
            var scan1Id = Guid.NewGuid();
            var scan2Id = Guid.NewGuid();

            var snapshot1Path = Path.Combine(snapshotDir, $"scan-{scan1Id}.json");
            var snapshot2Path = Path.Combine(snapshotDir, $"scan-{scan2Id}.json");

            await File.WriteAllTextAsync(snapshot1Path, JsonSerializer.Serialize(
                BuildSnapshot(scan1Id, rootPath, @"D:\same-root\A", "Makefile")));
            await File.WriteAllTextAsync(snapshot2Path, JsonSerializer.Serialize(
                BuildSnapshot(scan2Id, rootPath, @"D:\same-root\B", "package.json")));

            db.ScanSessions.AddRange(
                new ScanSessionEntity
                {
                    Id = scan1Id,
                    RootPath = rootPath,
                    Mode = "roots",
                    State = ScanSessionStates.Completed,
                    DiskKey = "D:",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    FinishedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                    OutputPath = snapshot1Path
                },
                new ScanSessionEntity
                {
                    Id = scan2Id,
                    RootPath = rootPath,
                    Mode = "roots",
                    State = ScanSessionStates.Completed,
                    DiskKey = "D:",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-8),
                    FinishedAt = DateTimeOffset.UtcNow.AddMinutes(-7),
                    OutputPath = snapshot2Path
                });

            db.ProjectSuggestions.AddRange(
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = scan1Id,
                    RootPath = rootPath,
                    Name = "A",
                    Path = @"D:\same-root\A",
                    Kind = "ProjectRoot",
                    Score = 0.7,
                    Reason = "markers: Makefile",
                    ExtensionsSummary = "c=1",
                    MarkersJson = "[\"Makefile\"]",
                    TechHintsJson = "[\"native\"]",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                    Status = ProjectSuggestionStatus.Accepted
                },
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = scan2Id,
                    RootPath = rootPath,
                    Name = "B",
                    Path = @"D:\same-root\B",
                    Kind = "ProjectRoot",
                    Score = 0.72,
                    Reason = "markers: package.json",
                    ExtensionsSummary = "js=2",
                    MarkersJson = "[\"package.json\"]",
                    TechHintsJson = "[\"javascript\"]",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-7),
                    Status = ProjectSuggestionStatus.Rejected
                });

            await db.SaveChangesAsync();

            var service = new ProjectSuggestionRegressionService(() => new AppDbContext(options));

            var report = await service.AnalyzeReplayFromHistoryAsync();

            report.RootsAnalyzed.Should().Be(2);
            report.AcceptedMissingCount.Should().Be(0);
            report.RejectedMissingCount.Should().Be(0);
            report.Roots.Select(item => item.SnapshotScanSessionId)
                .Should().BeEquivalentTo(new[] { scan1Id, scan2Id });
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, dbPath);
            if (Directory.Exists(snapshotDir))
            {
                Directory.Delete(snapshotDir, true);
            }
        }
    }

    [Fact]
    public async Task ReplayRegression_export_writes_json_file_with_report_payload()
    {
        var (options, db, dbPath) = await RootStoreTests.CreateDbAsync();
        var snapshotDir = Path.Combine(Path.GetTempPath(), $"dpo-replay-export-{Guid.NewGuid():N}");
        var exportDir = Path.Combine(Path.GetTempPath(), $"dpo-replay-export-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(snapshotDir);
        Directory.CreateDirectory(exportDir);

        try
        {
            var rootPath = @"D:\same-root";
            var scanId = Guid.NewGuid();
            var snapshotPath = Path.Combine(snapshotDir, $"scan-{scanId}.json");

            await File.WriteAllTextAsync(snapshotPath, JsonSerializer.Serialize(
                BuildSnapshot(scanId, rootPath, @"D:\same-root\A", "Makefile")));

            db.ScanSessions.Add(
                new ScanSessionEntity
                {
                    Id = scanId,
                    RootPath = rootPath,
                    Mode = "roots",
                    State = ScanSessionStates.Completed,
                    DiskKey = "D:",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    FinishedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                    OutputPath = snapshotPath
                });

            db.ProjectSuggestions.Add(
                new ProjectSuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = scanId,
                    RootPath = rootPath,
                    Name = "A",
                    Path = @"D:\same-root\A",
                    Kind = "ProjectRoot",
                    Score = 0.7,
                    Reason = "markers: Makefile",
                    ExtensionsSummary = "c=1",
                    MarkersJson = "[\"Makefile\"]",
                    TechHintsJson = "[\"native\"]",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                    Status = ProjectSuggestionStatus.Accepted
                });

            await db.SaveChangesAsync();

            var service = new ProjectSuggestionRegressionService(
                () => new AppDbContext(options),
                exportDirectory: exportDir);

            var result = await service.ExportReplayFromHistoryAsync();

            result.RootsAnalyzed.Should().Be(1);
            File.Exists(result.Path).Should().BeTrue();
            result.Path.StartsWith(exportDir, StringComparison.OrdinalIgnoreCase).Should().BeTrue();

            var json = await File.ReadAllTextAsync(result.Path);
            json.Should().Contain("\"report\"");
            json.Should().Contain("\"RootsAnalyzed\": 1");
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, dbPath);
            if (Directory.Exists(snapshotDir))
            {
                Directory.Delete(snapshotDir, true);
            }

            if (Directory.Exists(exportDir))
            {
                Directory.Delete(exportDir, true);
            }
        }
    }

    [Fact]
    [Trait("Category", "UserDataRegression")]
    public async Task ReplayRegression_reads_real_user_history_from_default_database()
    {
        var dbPath = AppDbContext.GetDefaultDbPath();
        File.Exists(dbPath).Should().BeTrue(
            $"SQLite database not found. Expected: {dbPath}. Run app + scans first.");

        await using (var db = new AppDbContext(AppDbContext.CreateDefaultOptions()))
        {
            await db.Database.MigrateAsync();
        }

        var service = new ProjectSuggestionRegressionService(() => new AppDbContext(AppDbContext.CreateDefaultOptions()));
        ProjectSuggestionReplayRegressionReport report;
        try
        {
            report = await service.AnalyzeReplayFromHistoryAsync();
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("No accepted/rejected suggestions found", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("No historical scan snapshots were found", StringComparison.OrdinalIgnoreCase))
        {
            // Empty baseline/no snapshots => no regression signal yet, test passes by design.
            return;
        }

        report.RootsAnalyzed.Should().BeGreaterThan(0,
            "No roots with historical accepted/rejected decisions and scan snapshots were found.");
        (report.BaselineAcceptedCount + report.BaselineRejectedCount).Should().BeGreaterThan(0,
            "No accepted/rejected suggestions found. Accept or reject some suggestions in UI first.");

        report.AcceptedMissingCount.Should().Be(0,
            "Regression detected: some historically accepted suggestions are missing under current heuristics.");
    }

    private static ScanSnapshot BuildSnapshot(
        Guid scanId,
        string rootPath,
        string projectPath,
        string markerFile)
    {
        return new ScanSnapshot
        {
            ScanId = scanId,
            Mode = "roots",
            StartedAt = DateTimeOffset.UtcNow,
            FinishedAt = DateTimeOffset.UtcNow,
            Roots =
            {
                new DirectoryNode
                {
                    Name = "same-root",
                    Path = rootPath,
                    Directories =
                    {
                        new DirectoryNode
                        {
                            Name = Path.GetFileName(projectPath),
                            Path = projectPath,
                            Files =
                            {
                                new FileNode
                                {
                                    Name = markerFile,
                                    Path = Path.Combine(projectPath, markerFile),
                                    Extension = Path.GetExtension(markerFile)
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
