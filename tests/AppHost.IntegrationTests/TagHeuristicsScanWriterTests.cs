using System.Text.Json;
using AppHost.Services;
using FluentAssertions;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class TagHeuristicsScanWriterTests
{
    [Fact]
    public async Task SaveAsync_writes_scan_json_with_expected_filename()
    {
        var testDataDir = Path.Combine(Path.GetTempPath(), $"dpo-tag-heur-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDataDir);

        try
        {
            var runId = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
            var snapshot = new TagHeuristicsScanSnapshot
            {
                RunId = runId,
                ProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ProjectName = "sample-project",
                ProjectPath = @"D:\code\sample-project",
                StartedAt = DateTimeOffset.UtcNow.AddSeconds(-2),
                FinishedAt = DateTimeOffset.UtcNow,
                AvailableTags = 10,
                DetectedSuggestions = 2,
                GeneratedSuggestions = 1,
                Suggestions =
                [
                    new TagHeuristicsScanSuggestion
                    {
                        TagId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                        TagName = "csharp",
                        Type = "AssignExisting",
                        Source = "Heuristic",
                        Confidence = 0.82,
                        Reason = "marker:.csproj",
                        Fingerprint = "fp-1",
                        CreatedAt = DateTimeOffset.UtcNow
                    }
                ]
            };

            var writer = new TagHeuristicsScanWriter(testDataDir);

            var outputPath = await writer.SaveAsync(snapshot, runId, CancellationToken.None);

            outputPath.Should().EndWith(Path.Combine("scans", "scan-tag-heur-12345678-1234-1234-1234-1234567890ab.json"));
            File.Exists(outputPath).Should().BeTrue();

            var json = await File.ReadAllTextAsync(outputPath);
            var roundtrip = JsonSerializer.Deserialize<TagHeuristicsScanSnapshot>(json);
            roundtrip.Should().NotBeNull();
            roundtrip!.RunId.Should().Be(runId);
            roundtrip.GeneratedSuggestions.Should().Be(1);
            roundtrip.Suggestions.Should().ContainSingle(item => item.TagName == "csharp");
        }
        finally
        {
            if (Directory.Exists(testDataDir))
            {
                Directory.Delete(testDataDir, true);
            }
        }
    }
}
