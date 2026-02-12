using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectSuggestionRegressionServiceTests
{
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
        report.BaselineAcceptedCount.Should().BeGreaterThan(0,
            "No accepted suggestions found. Accept or reject some suggestions in UI first.");

        report.AcceptedMissingCount.Should().Be(0,
            "Regression detected: some historically accepted suggestions are missing under current heuristics.");
    }
}
