using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppHost.Persistence;

namespace AppHost.Services;

public sealed class TagSuggestionHeuristicsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IReadOnlyList<DetectedTagSuggestion> Detect(ProjectEntity project, IReadOnlyList<TagEntity> tags)
    {
        if (tags.Count == 0)
        {
            return [];
        }

        var tagsByNormalized = tags.ToDictionary(
            tag => tag.NormalizedName,
            tag => tag,
            StringComparer.OrdinalIgnoreCase);

        var evidenceByTag = new Dictionary<string, TagSignal>(StringComparer.OrdinalIgnoreCase);

        var markers = DeserializeStringList(project.MarkersJson);
        var hints = DeserializeStringList(project.TechHintsJson);
        var extensionHistogram = ParseExtensionSummary(project.ExtensionsSummary);

        foreach (var marker in markers)
        {
            var normalized = marker.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case ".sln":
                    AddSignal("vs-solution", 0.93, $"marker:{normalized}");
                    break;
                case ".csproj":
                    AddSignal("vs-project", 0.91, $"marker:{normalized}");
                    AddSignal("csharp", 0.82, $"marker:{normalized}");
                    AddSignal("dotnet", 0.82, $"marker:{normalized}");
                    break;
                case ".vcxproj":
                case ".vcproj":
                    AddSignal("vs-project", 0.91, $"marker:{normalized}");
                    AddSignal("cpp", 0.84, $"marker:{normalized}");
                    AddSignal("native", 0.8, $"marker:{normalized}");
                    AddSignal("low-level", 0.66, $"marker:{normalized}");
                    break;
                case "package.json":
                    AddSignal("node", 0.91, $"marker:{normalized}");
                    break;
                case "cmakelists.txt":
                    AddSignal("cmake", 0.91, $"marker:{normalized}");
                    AddSignal("cpp", 0.72, $"marker:{normalized}");
                    break;
                case "makefile":
                    AddSignal("makefile", 0.91, $"marker:{normalized}");
                    AddSignal("native", 0.68, $"marker:{normalized}");
                    break;
                case "pom.xml":
                    AddSignal("maven", 0.91, $"marker:{normalized}");
                    AddSignal("java", 0.76, $"marker:{normalized}");
                    break;
                case "build.gradle":
                    AddSignal("gradle", 0.91, $"marker:{normalized}");
                    AddSignal("java", 0.76, $"marker:{normalized}");
                    break;
                case ".git":
                    AddSignal("git", 0.78, $"marker:{normalized}");
                    break;
                case "index.html":
                    AddSignal("html", 0.87, $"marker:{normalized}");
                    AddSignal("gui", 0.6, $"marker:{normalized}");
                    break;
                case "main-source":
                    AddSignal("console", 0.73, $"marker:{normalized}");
                    break;
                case "single-source-file":
                    AddSignal("console", 0.56, $"marker:{normalized}");
                    break;
            }
        }

        foreach (var hint in hints)
        {
            var normalized = hint.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "cpp":
                    AddSignal("cpp", 0.84, $"hint:{normalized}");
                    AddSignal("low-level", 0.7, $"hint:{normalized}");
                    break;
                case "native":
                    AddSignal("native", 0.8, $"hint:{normalized}");
                    AddSignal("low-level", 0.7, $"hint:{normalized}");
                    break;
                case "csharp":
                    AddSignal("csharp", 0.82, $"hint:{normalized}");
                    break;
                case ".net":
                case "dotnet":
                    AddSignal("dotnet", 0.82, $"hint:{normalized}");
                    break;
                case "node":
                    AddSignal("node", 0.82, $"hint:{normalized}");
                    break;
                case "python":
                case "rust":
                case "go":
                case "powershell":
                case "java":
                case "angular":
                case "react":
                case "html":
                    AddSignal(normalized, 0.76, $"hint:{normalized}");
                    break;
            }
        }

        foreach (var (ext, count) in extensionHistogram)
        {
            switch (ext)
            {
                case "cpp":
                case "cc":
                case "cxx":
                    AddSignal("cpp", Math.Min(0.82, 0.58 + count * 0.03), $"ext:{ext}={count}");
                    AddSignal("native", Math.Min(0.76, 0.55 + count * 0.02), $"ext:{ext}={count}");
                    break;
                case "c":
                    AddSignal("c", Math.Min(0.82, 0.58 + count * 0.03), $"ext:{ext}={count}");
                    AddSignal("native", Math.Min(0.72, 0.53 + count * 0.02), $"ext:{ext}={count}");
                    break;
                case "cs":
                    AddSignal("csharp", Math.Min(0.82, 0.58 + count * 0.03), $"ext:{ext}={count}");
                    AddSignal("dotnet", Math.Min(0.76, 0.55 + count * 0.02), $"ext:{ext}={count}");
                    break;
                case "html":
                    AddSignal("html", Math.Min(0.82, 0.58 + count * 0.02), $"ext:{ext}={count}");
                    AddSignal("gui", Math.Min(0.68, 0.5 + count * 0.02), $"ext:{ext}={count}");
                    break;
                case "ps1":
                    AddSignal("powershell", Math.Min(0.82, 0.58 + count * 0.03), $"ext:{ext}={count}");
                    break;
                case "go":
                case "rs":
                case "py":
                    var mapped = ext switch
                    {
                        "go" => "go",
                        "rs" => "rust",
                        "py" => "python",
                        _ => ext
                    };
                    AddSignal(mapped, Math.Min(0.82, 0.58 + count * 0.03), $"ext:{ext}={count}");
                    break;
            }
        }

        var pathLower = project.Path.ToLowerInvariant();
        if (pathLower.Contains("winapi", StringComparison.OrdinalIgnoreCase))
        {
            AddSignal("winapi", 0.75, "path:winapi");
            AddSignal("native", 0.62, "path:winapi");
        }

        if (pathLower.Contains("swing", StringComparison.OrdinalIgnoreCase)
            || pathLower.Contains(@"\gui", StringComparison.OrdinalIgnoreCase))
        {
            AddSignal("gui", 0.65, "path:gui");
        }

        if (pathLower.Contains("console", StringComparison.OrdinalIgnoreCase))
        {
            AddSignal("console", 0.62, "path:console");
        }

        if (project.Reason.Contains("native sources", StringComparison.OrdinalIgnoreCase))
        {
            AddSignal("native", 0.72, "reason:native-sources");
            AddSignal("console", 0.64, "reason:native-sources");
        }

        if (project.Reason.Contains("single file candidate", StringComparison.OrdinalIgnoreCase))
        {
            AddSignal("console", 0.58, "reason:single-file");
        }

        if (project.Reason.Contains("static site", StringComparison.OrdinalIgnoreCase))
        {
            AddSignal("html", 0.76, "reason:static-site");
            AddSignal("gui", 0.62, "reason:static-site");
        }

        var output = new List<DetectedTagSuggestion>();
        foreach (var (normalizedTag, signal) in evidenceByTag)
        {
            if (signal.Confidence < 0.55)
            {
                continue;
            }

            if (!tagsByNormalized.TryGetValue(normalizedTag, out var existingTag))
            {
                continue;
            }

            var fingerprint = BuildFingerprint(project.Id, normalizedTag, signal.Evidences);
            output.Add(new DetectedTagSuggestion(
                existingTag.Id,
                existingTag.Name,
                TagSuggestionType.AssignExisting.ToString(),
                TagSuggestionSource.Heuristic.ToString(),
                Math.Round(signal.Confidence, 2),
                BuildReason(signal.Evidences),
                fingerprint,
                DateTimeOffset.UtcNow));
        }

        return output
            .OrderByDescending(item => item.Confidence)
            .ThenBy(item => item.TagName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        void AddSignal(string normalizedTag, double confidence, string evidence)
        {
            if (!evidenceByTag.TryGetValue(normalizedTag, out var signal))
            {
                signal = new TagSignal();
                evidenceByTag[normalizedTag] = signal;
            }

            signal.Evidences.Add(evidence);
            signal.Confidence = Math.Max(signal.Confidence, confidence);
            if (signal.Evidences.Count > 1)
            {
                signal.Confidence = Math.Min(0.99, signal.Confidence + Math.Min(0.12, (signal.Evidences.Count - 1) * 0.03));
            }
        }
    }

    private static string BuildReason(IReadOnlyCollection<string> evidences)
    {
        return string.Join(", ", evidences.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).Take(3));
    }

    private static string BuildFingerprint(Guid projectId, string tag, IReadOnlyCollection<string> evidences)
    {
        var builder = new StringBuilder();
        builder.Append("v1|");
        builder.Append(projectId);
        builder.Append('|');
        builder.Append(tag.ToLowerInvariant());
        builder.Append('|');
        foreach (var evidence in evidences.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(evidence.ToLowerInvariant());
            builder.Append('|');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IReadOnlyList<string> DeserializeStringList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyDictionary<string, int> ParseExtensionSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var histogram = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in summary.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = segment.Split('=', StringSplitOptions.TrimEntries);
            if (split.Length != 2)
            {
                continue;
            }

            if (!int.TryParse(split[1], out var value))
            {
                continue;
            }

            var key = split[0].Trim().TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            histogram[key] = value;
        }

        return histogram;
    }

    private sealed class TagSignal
    {
        public double Confidence { get; set; }
        public HashSet<string> Evidences { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
