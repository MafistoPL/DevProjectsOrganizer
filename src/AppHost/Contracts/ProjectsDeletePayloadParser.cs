using System.Text.Json;

namespace AppHost.Contracts;

public static class ProjectsDeletePayloadParser
{
    public static bool TryParse(JsonElement? payload, out Guid projectId)
    {
        projectId = Guid.Empty;
        if (!payload.HasValue)
        {
            return false;
        }

        var element = payload.Value;
        if (!element.TryGetProperty("projectId", out var idElement))
        {
            return false;
        }

        return Guid.TryParse(idElement.GetString(), out projectId);
    }
}
