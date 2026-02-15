using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using System.IO;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class TagSuggestionHeuristicsServiceTests
{
    [Fact]
    public void Detect_returns_assign_existing_suggestions_for_known_tags()
    {
        var project = CreateProject(
            markersJson: """[".sln",".vcxproj"]""",
            techHintsJson: """["cpp","native"]""",
            extensionsSummary: "cpp=4,h=2");

        var tags =
            new[]
            {
                CreateTag("vs-solution"),
                CreateTag("vs-project"),
                CreateTag("cpp"),
                CreateTag("native"),
                CreateTag("low-level")
            };

        var sut = new TagSuggestionHeuristicsService();

        var result = sut.Detect(project, tags);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(item => item.Type == "AssignExisting");
        result.Should().OnlyContain(item => item.Source == "Heuristic");
        result.Select(item => item.TagName).Should().Contain(new[] { "vs-solution", "vs-project", "cpp", "native" });
    }

    [Fact]
    public void Detect_ignores_signals_without_existing_tag()
    {
        var project = CreateProject(
            markersJson: """[".sln","package.json"]""",
            techHintsJson: """["node"]""",
            extensionsSummary: "js=10");

        var tags =
            new[]
            {
                CreateTag("cpp"),
                CreateTag("native")
            };

        var sut = new TagSuggestionHeuristicsService();

        var result = sut.Detect(project, tags);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Detect_generates_stable_fingerprint_for_same_project_and_tag()
    {
        var project = CreateProject(
            markersJson: """[".csproj"]""",
            techHintsJson: """["csharp",".net"]""",
            extensionsSummary: "cs=12,json=2");

        var tags =
            new[]
            {
                CreateTag("csharp"),
                CreateTag("dotnet"),
                CreateTag("vs-project")
            };

        var sut = new TagSuggestionHeuristicsService();

        var first = sut.Detect(project, tags)
            .OrderBy(item => item.TagName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var second = sut.Detect(project, tags)
            .OrderBy(item => item.TagName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        first.Select(item => item.Fingerprint).Should().Equal(second.Select(item => item.Fingerprint));
    }

    [Fact]
    public void Detect_adds_hello_world_for_beginner_chapter_path_when_tag_exists()
    {
        var project = CreateProject(
            markersJson: """["main-source"]""",
            techHintsJson: """["c","native"]""",
            extensionsSummary: "c=2,h=1",
            name: "Chapter_01",
            path: @"D:\z-pulpitu\ProgrammingLearning\one_drive\Old_Projects\Beginning_C 1\Chapter_01",
            reason: "native sources with entry/header layout");

        var tags = new[]
        {
            CreateTag("hello-world"),
            CreateTag("native")
        };

        var sut = new TagSuggestionHeuristicsService();

        var result = sut.Detect(project, tags);

        result.Select(item => item.TagName).Should().Contain("hello-world");
        result.Should().Contain(item =>
            item.TagName == "hello-world"
            && item.Reason.Contains("path:beginner-chapter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Detect_adds_hello_world_and_lorem_ipsum_based_on_source_content()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dpo-tag-heur-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var sourcePath = Path.Combine(tempDir, "main.c");
            File.WriteAllText(
                sourcePath,
                """
                #include <stdio.h>
                int main(void) {
                    printf("Hello, world!\n");
                    // Lorem ipsum dolor sit amet.
                    return 0;
                }
                """);

            var project = CreateProject(
                markersJson: """["single-source-file"]""",
                techHintsJson: """["c"]""",
                extensionsSummary: "c=1",
                name: "sample",
                path: tempDir,
                reason: "single file candidate: main.c");

            var tags = new[]
            {
                CreateTag("hello-world"),
                CreateTag("lorem-ipsum")
            };

            var sut = new TagSuggestionHeuristicsService();

            var result = sut.Detect(project, tags);

            result.Select(item => item.TagName).Should().Contain(new[] { "hello-world", "lorem-ipsum" });
            result.Should().Contain(item =>
                item.TagName == "hello-world"
                && item.Reason.Contains("code:hello-world", StringComparison.OrdinalIgnoreCase));
            result.Should().Contain(item =>
                item.TagName == "lorem-ipsum"
                && item.Reason.Contains("code:lorem-ipsum", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static ProjectEntity CreateProject(
        string markersJson,
        string techHintsJson,
        string extensionsSummary,
        string name = "sample",
        string path = @"D:\code\sample",
        string reason = ".sln + marker")
    {
        var now = DateTimeOffset.UtcNow;
        var kind = "ProjectRoot";

        return new ProjectEntity
        {
            Id = Guid.NewGuid(),
            SourceSuggestionId = Guid.NewGuid(),
            LastScanSessionId = Guid.NewGuid(),
            RootPath = @"D:\code",
            Name = name,
            Path = path,
            Kind = kind,
            ProjectKey = ProjectStore.BuildProjectKey(path, kind),
            Score = 0.88,
            Reason = reason,
            ExtensionsSummary = extensionsSummary,
            MarkersJson = markersJson,
            TechHintsJson = techHintsJson,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static TagEntity CreateTag(string name)
    {
        var now = DateTimeOffset.UtcNow;
        return new TagEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
