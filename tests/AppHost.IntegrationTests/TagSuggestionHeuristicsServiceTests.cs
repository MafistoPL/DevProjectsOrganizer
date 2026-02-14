using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
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

    private static ProjectEntity CreateProject(string markersJson, string techHintsJson, string extensionsSummary)
    {
        var now = DateTimeOffset.UtcNow;
        var path = @"D:\code\sample";
        var kind = "ProjectRoot";

        return new ProjectEntity
        {
            Id = Guid.NewGuid(),
            SourceSuggestionId = Guid.NewGuid(),
            LastScanSessionId = Guid.NewGuid(),
            RootPath = @"D:\code",
            Name = "sample",
            Path = path,
            Kind = kind,
            ProjectKey = ProjectStore.BuildProjectKey(path, kind),
            Score = 0.88,
            Reason = ".sln + marker",
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
