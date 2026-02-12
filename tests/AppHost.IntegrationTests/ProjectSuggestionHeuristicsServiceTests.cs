using AppHost.Services;
using FluentAssertions;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectSuggestionHeuristicsServiceTests
{
    [Fact]
    public void Detect_creates_suggestion_when_marker_exists()
    {
        var snapshot = new ScanSnapshot
        {
            Roots =
            {
                new DirectoryNode
                {
                    Name = "dotnet-api",
                    Path = @"D:\code\dotnet-api",
                    Files =
                    {
                        new FileNode { Name = "Api.sln", Extension = ".sln" },
                        new FileNode { Name = "Api.csproj", Extension = ".csproj" },
                        new FileNode { Name = "Program.cs", Extension = ".cs" }
                    }
                }
            }
        };

        var sut = new ProjectSuggestionHeuristicsService();

        var results = sut.Detect(snapshot);

        results.Should().ContainSingle();
        var suggestion = results[0];
        suggestion.Name.Should().Be("dotnet-api");
        suggestion.Path.Should().Be(@"D:\code\dotnet-api");
        suggestion.Markers.Should().Contain(".sln");
        suggestion.Markers.Should().Contain(".csproj");
        suggestion.TechHints.Should().Contain("csharp");
        suggestion.ExtensionsSummary.Should().Contain("cs=");
    }

    [Fact]
    public void Detect_skips_directory_without_markers()
    {
        var snapshot = new ScanSnapshot
        {
            Roots =
            {
                new DirectoryNode
                {
                    Name = "notes",
                    Path = @"D:\notes",
                    Files =
                    {
                        new FileNode { Name = "readme.md", Extension = ".md" },
                        new FileNode { Name = "todo.txt", Extension = ".txt" }
                    }
                }
            }
        };

        var sut = new ProjectSuggestionHeuristicsService();

        var results = sut.Detect(snapshot);

        results.Should().BeEmpty();
    }
}
