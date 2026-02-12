using System.IO;
using System.Linq;

namespace AppHost.Services;

public sealed record DetectedProjectSuggestion(
    string Name,
    string Path,
    string Kind,
    double Score,
    string Reason,
    string ExtensionsSummary,
    IReadOnlyList<string> Markers,
    IReadOnlyList<string> TechHints,
    DateTimeOffset CreatedAt
);

public sealed class ProjectSuggestionHeuristicsService
{
    private static readonly HashSet<string> MarkerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json",
        "CMakeLists.txt",
        "Makefile",
        "pom.xml",
        "build.gradle"
    };

    public IReadOnlyList<DetectedProjectSuggestion> Detect(ScanSnapshot snapshot)
    {
        var suggestions = new List<DetectedProjectSuggestion>();
        foreach (var root in snapshot.Roots)
        {
            Collect(root, suggestions);
        }

        return suggestions
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, int> Collect(
        DirectoryNode node,
        List<DetectedProjectSuggestion> suggestions)
    {
        var histogram = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var child in node.Directories)
        {
            var childHistogram = Collect(child, suggestions);
            Merge(histogram, childHistogram);
        }

        foreach (var file in node.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Extension))
            {
                continue;
            }

            var normalized = file.Extension.Trim().TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            histogram[normalized] = histogram.GetValueOrDefault(normalized) + 1;
        }

        var markers = DetectMarkers(node);
        if (markers.Count > 0)
        {
            suggestions.Add(BuildSuggestion(node, markers, histogram));
        }

        return histogram;
    }

    private static IReadOnlyList<string> DetectMarkers(DirectoryNode node)
    {
        var markers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in node.Directories)
        {
            if (string.Equals(directory.Name, ".git", StringComparison.OrdinalIgnoreCase))
            {
                markers.Add(".git");
            }
        }

        foreach (var file in node.Files)
        {
            if (MarkerNames.Contains(file.Name))
            {
                markers.Add(file.Name);
            }

            if (file.Name.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                markers.Add(".sln");
            }

            if (file.Name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                markers.Add(".csproj");
            }
        }

        return markers
            .OrderBy(marker => marker, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DetectedProjectSuggestion BuildSuggestion(
        DirectoryNode node,
        IReadOnlyList<string> markers,
        Dictionary<string, int> histogram)
    {
        var extensionSummary = string.Join(
            ", ",
            histogram
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(pair => $"{pair.Key}={pair.Value}"));

        var normalizedName = NormalizeName(node.Name, node.Path);
        var reason = $"markers: {string.Join(", ", markers)}";
        var score = CalculateScore(markers);
        var techHints = BuildTechHints(markers, histogram);

        return new DetectedProjectSuggestion(
            normalizedName,
            node.Path,
            "ProjectRoot",
            score,
            reason,
            extensionSummary,
            markers,
            techHints,
            DateTimeOffset.UtcNow);
    }

    private static string NormalizeName(string name, string path)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed);
    }

    private static double CalculateScore(IReadOnlyList<string> markers)
    {
        var score = 0.52;
        foreach (var marker in markers)
        {
            score += marker switch
            {
                ".sln" => 0.2,
                ".csproj" => 0.18,
                "package.json" => 0.16,
                "CMakeLists.txt" => 0.15,
                "Makefile" => 0.1,
                "pom.xml" => 0.16,
                "build.gradle" => 0.16,
                ".git" => 0.06,
                _ => 0.04
            };
        }

        return Math.Round(Math.Min(0.99, score), 2);
    }

    private static IReadOnlyList<string> BuildTechHints(
        IReadOnlyList<string> markers,
        Dictionary<string, int> histogram)
    {
        var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var marker in markers)
        {
            switch (marker)
            {
                case ".sln":
                case ".csproj":
                    hints.Add("csharp");
                    hints.Add(".net");
                    break;
                case "package.json":
                    hints.Add("node");
                    hints.Add("javascript");
                    break;
                case "CMakeLists.txt":
                    hints.Add("cmake");
                    hints.Add("cpp");
                    break;
                case "Makefile":
                    hints.Add("native");
                    break;
                case "pom.xml":
                case "build.gradle":
                    hints.Add("java");
                    break;
            }
        }

        if (histogram.ContainsKey("ts"))
        {
            hints.Add("typescript");
        }

        if (histogram.ContainsKey("py"))
        {
            hints.Add("python");
        }

        if (histogram.ContainsKey("go"))
        {
            hints.Add("go");
        }

        if (histogram.ContainsKey("rs"))
        {
            hints.Add("rust");
        }

        return hints
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Merge(Dictionary<string, int> target, Dictionary<string, int> source)
    {
        foreach (var (key, value) in source)
        {
            target[key] = target.GetValueOrDefault(key) + value;
        }
    }
}
