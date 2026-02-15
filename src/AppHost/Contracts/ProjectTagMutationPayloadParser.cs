using System.Text.Json;

namespace AppHost.Contracts;

public static class ProjectTagMutationPayloadParser
{
    public static bool TryParse(JsonElement? payload, out Guid projectId, out Guid tagId)
    {
        projectId = Guid.Empty;
        tagId = Guid.Empty;
        if (!payload.HasValue)
        {
            return false;
        }

        var element = payload.Value;
        if (!element.TryGetProperty("projectId", out var projectIdElement))
        {
            return false;
        }

        if (!element.TryGetProperty("tagId", out var tagIdElement))
        {
            return false;
        }

        return Guid.TryParse(projectIdElement.GetString(), out projectId)
            && Guid.TryParse(tagIdElement.GetString(), out tagId);
    }
}
