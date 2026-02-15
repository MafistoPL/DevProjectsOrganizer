using System.Text.Json;
using AppHost.Contracts;
using FluentAssertions;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectTagMutationPayloadParserTests
{
    [Fact]
    public void TryParse_accepts_minimal_payload()
    {
        var projectId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        using var doc = JsonDocument.Parse($$"""
            {
              "projectId": "{{projectId}}",
              "tagId": "{{tagId}}"
            }
            """);

        var ok = ProjectTagMutationPayloadParser.TryParse(doc.RootElement, out var parsedProjectId, out var parsedTagId);

        ok.Should().BeTrue();
        parsedProjectId.Should().Be(projectId);
        parsedTagId.Should().Be(tagId);
    }

    [Fact]
    public void TryParse_accepts_payload_with_extra_fields()
    {
        var projectId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        using var doc = JsonDocument.Parse($$"""
            {
              "projectId": "{{projectId}}",
              "tagId": "{{tagId}}",
              "foo": "bar"
            }
            """);

        var ok = ProjectTagMutationPayloadParser.TryParse(doc.RootElement, out var parsedProjectId, out var parsedTagId);

        ok.Should().BeTrue();
        parsedProjectId.Should().Be(projectId);
        parsedTagId.Should().Be(tagId);
    }

    [Fact]
    public void TryParse_rejects_payload_without_project_id()
    {
        var tagId = Guid.NewGuid();
        using var doc = JsonDocument.Parse($$"""
            {
              "tagId": "{{tagId}}"
            }
            """);

        var ok = ProjectTagMutationPayloadParser.TryParse(doc.RootElement, out var parsedProjectId, out var parsedTagId);

        ok.Should().BeFalse();
        parsedProjectId.Should().Be(Guid.Empty);
        parsedTagId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void TryParse_rejects_payload_without_tag_id()
    {
        var projectId = Guid.NewGuid();
        using var doc = JsonDocument.Parse($$"""
            {
              "projectId": "{{projectId}}"
            }
            """);

        var ok = ProjectTagMutationPayloadParser.TryParse(doc.RootElement, out var parsedProjectId, out var parsedTagId);

        ok.Should().BeFalse();
        parsedProjectId.Should().Be(Guid.Empty);
        parsedTagId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void TryParse_rejects_payload_with_invalid_guid()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "projectId": "not-guid",
              "tagId": "also-not-guid"
            }
            """);

        var ok = ProjectTagMutationPayloadParser.TryParse(doc.RootElement, out var parsedProjectId, out var parsedTagId);

        ok.Should().BeFalse();
        parsedProjectId.Should().Be(Guid.Empty);
        parsedTagId.Should().Be(Guid.Empty);
    }
}
