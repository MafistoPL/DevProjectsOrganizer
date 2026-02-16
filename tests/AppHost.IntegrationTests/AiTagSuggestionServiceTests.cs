using AppHost.Persistence;
using AppHost.Services;
using FluentAssertions;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class AiTagSuggestionServiceTests
{
    [Fact]
    public void Detect_matches_custom_tag_from_project_metadata()
    {
        var service = new AiTagSuggestionService();
        var project = CreateProject(
            name: "aleksandra-wiejaczka-strona",
            path: @"D:\web\aleksandra-wiejaczka-strona",
            description: "Landing page for Aleksandra");

        var tags = new[]
        {
            CreateTag("strona"),
            CreateTag("desktop")
        };

        var detected = service.Detect(project, tags);

        detected.Should().ContainSingle(item => item.TagName == "strona");
        detected.Should().Contain(item =>
            item.TagName == "strona"
            && string.Equals(item.Source, TagSuggestionSource.Ai.ToString(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.Type, TagSuggestionType.AssignExisting.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Detect_returns_empty_for_unrelated_custom_tags()
    {
        var service = new AiTagSuggestionService();
        var project = CreateProject(
            name: "dotnet-api",
            path: @"D:\code\dotnet-api",
            description: "Backend service");

        var tags = new[]
        {
            CreateTag("android"),
            CreateTag("unity")
        };

        var detected = service.Detect(project, tags);
        detected.Should().BeEmpty();
    }

    private static ProjectEntity CreateProject(string name, string path, string description)
    {
        return new ProjectEntity
        {
            Id = Guid.NewGuid(),
            SourceSuggestionId = Guid.NewGuid(),
            LastScanSessionId = Guid.NewGuid(),
            RootPath = @"D:\code",
            Name = name,
            Path = path,
            Kind = "ProjectRoot",
            ProjectKey = ProjectStore.BuildProjectKey(path, "ProjectRoot"),
            Description = description,
            FileCount = 1,
            Score = 0.8,
            Reason = "markers: package.json",
            ExtensionsSummary = "ts=6",
            MarkersJson = "[\"package.json\"]",
            TechHintsJson = "[\"node\"]",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static TagEntity CreateTag(string name)
    {
        return new TagEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.Trim().ToLowerInvariant(),
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
