using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ScanCoordinatorTests
{
    [Fact]
    public async Task StartAsync_creates_snapshot_json_for_root_scan()
    {
        var fixtureRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DevProjectsOrganizer",
            "test-fixtures",
            $"scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureRoot);

        var sourceDir = Path.Combine(fixtureRoot, "src");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "Program.cs"), "line1\nline2\nline3\n");
        await File.WriteAllTextAsync(Path.Combine(fixtureRoot, "Makefile"), "all:\n\techo ok\n");

        var (options, db, path) = await RootStoreTests.CreateDbAsync();
        try
        {
            var store = new RootStore(db);
            var root = await store.AddAsync(fixtureRoot);

            var coordinator = new ScanCoordinator(() => new AppDbContext(options));
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
        }
        finally
        {
            await RootStoreTests.DisposeDbAsync(db, path);
            await DeleteScanOutputAsync();
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, true);
            }
        }
    }

    private static Task DeleteScanOutputAsync()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DevProjectsOrganizer",
            "scans");
        if (!Directory.Exists(dataDir))
        {
            return Task.CompletedTask;
        }

        foreach (var file in Directory.EnumerateFiles(dataDir, "scan-*.json"))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // ignore cleanup issues
            }
        }

        return Task.CompletedTask;
    }
}
