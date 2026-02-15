using System.Text.Json;
using AppHost.Contracts;
using FluentAssertions;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectsRescanPayloadParserTests
{
    [Fact]
    public void TryParse_accepts_minimal_payload()
    {
        var projectId = Guid.NewGuid();
        using var doc = JsonDocument.Parse($$"""
            {
              "projectId": "{{projectId}}"
            }
            """);

        var ok = ProjectsRescanPayloadParser.TryParse(doc.RootElement, out var parsedProjectId);

        ok.Should().BeTrue();
        parsedProjectId.Should().Be(projectId);
    }

    [Fact]
    public void TryParse_accepts_payload_with_extra_fields()
    {
        var projectId = Guid.NewGuid();
        using var doc = JsonDocument.Parse($$"""
            {
              "projectId": "{{projectId}}",
              "foo": "bar"
            }
            """);

        var ok = ProjectsRescanPayloadParser.TryParse(doc.RootElement, out var parsedProjectId);

        ok.Should().BeTrue();
        parsedProjectId.Should().Be(projectId);
    }

    [Fact]
    public void TryParse_rejects_payload_without_project_id()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "foo": "bar"
            }
            """);

        var ok = ProjectsRescanPayloadParser.TryParse(doc.RootElement, out var parsedProjectId);

        ok.Should().BeFalse();
        parsedProjectId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void TryParse_rejects_payload_with_invalid_project_id()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "projectId": "not-guid"
            }
            """);

        var ok = ProjectsRescanPayloadParser.TryParse(doc.RootElement, out var parsedProjectId);

        ok.Should().BeFalse();
        parsedProjectId.Should().Be(Guid.Empty);
    }
}
