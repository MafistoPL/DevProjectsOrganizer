using System.Text.Json;
using AppHost.Contracts;
using FluentAssertions;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectsDeletePayloadParserTests
{
    [Fact]
    public void TryParse_accepts_payload_with_project_id_only()
    {
        var projectId = Guid.NewGuid();
        using var doc = JsonDocument.Parse($$"""
            {
              "projectId": "{{projectId}}"
            }
            """);

        var ok = ProjectsDeletePayloadParser.TryParse(doc.RootElement, out var parsedId);

        ok.Should().BeTrue();
        parsedId.Should().Be(projectId);
    }

    [Fact]
    public void TryParse_accepts_payload_with_project_id_and_extra_fields()
    {
        var projectId = Guid.NewGuid();
        using var doc = JsonDocument.Parse($$"""
            {
              "projectId": "{{projectId}}",
              "confirmName": "dotnet-api"
            }
            """);

        var ok = ProjectsDeletePayloadParser.TryParse(doc.RootElement, out var parsedId);

        ok.Should().BeTrue();
        parsedId.Should().Be(projectId);
    }

    [Fact]
    public void TryParse_rejects_payload_without_project_id()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "confirmName": "dotnet-api"
            }
            """);

        var ok = ProjectsDeletePayloadParser.TryParse(doc.RootElement, out var parsedId);

        ok.Should().BeFalse();
        parsedId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void TryParse_rejects_payload_with_invalid_project_id()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "projectId": "not-a-guid"
            }
            """);

        var ok = ProjectsDeletePayloadParser.TryParse(doc.RootElement, out var parsedId);

        ok.Should().BeFalse();
        parsedId.Should().Be(Guid.Empty);
    }
}
