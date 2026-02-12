using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
    string Fingerprint,
    DateTimeOffset CreatedAt
);

public sealed class ProjectSuggestionHeuristicsService
{
    private sealed record SourceFileInfo(FileNode File, string Ext);

    private static readonly HashSet<string> MarkerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json",
        "CMakeLists.txt",
        "Makefile",
        "pom.xml",
        "build.gradle"
    };
    private static readonly HashSet<string> SingleFileProjectExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "c", "cpp", "cc", "cxx", "cs", "py", "js", "ts", "java", "go", "rs", "ps1"
    };
    private static readonly HashSet<string> NativeSourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "c", "cpp", "cc", "cxx"
    };
    private static readonly HashSet<string> NativeHeaderExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "h", "hpp", "hh", "hxx"
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
            return histogram;
        }

        if (ShouldSkipHeuristicCandidate(node))
        {
            return histogram;
        }

        var heuristicSuggestion = BuildHeuristicSuggestion(node, histogram);
        if (heuristicSuggestion != null)
        {
            suggestions.Add(heuristicSuggestion);
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

            if (file.Name.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase))
            {
                markers.Add(".vcxproj");
            }

            if (file.Name.EndsWith(".vcproj", StringComparison.OrdinalIgnoreCase))
            {
                markers.Add(".vcproj");
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
        var fingerprint = BuildFingerprint(node, "ProjectRoot", markers, histogram);

        return new DetectedProjectSuggestion(
            normalizedName,
            node.Path,
            "ProjectRoot",
            score,
            reason,
            extensionSummary,
            markers,
            techHints,
            fingerprint,
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
                ".vcxproj" => 0.18,
                ".vcproj" => 0.18,
                "package.json" => 0.16,
                "CMakeLists.txt" => 0.15,
                "Makefile" => 0.1,
                "pom.xml" => 0.16,
                "build.gradle" => 0.16,
                "index.html" => 0.08,
                "main-source" => 0.12,
                "single-source-file" => 0.1,
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
                case ".csproj":
                    hints.Add("csharp");
                    hints.Add(".net");
                    break;
                case ".vcxproj":
                case ".vcproj":
                    hints.Add("cpp");
                    hints.Add("native");
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

    private static DetectedProjectSuggestion? BuildHeuristicSuggestion(
        DirectoryNode node,
        Dictionary<string, int> histogram)
    {
        var files = node.Files;
        var sourceFiles = files
            .Where(file => !string.IsNullOrWhiteSpace(file.Extension))
            .Select(file => new SourceFileInfo(file, file.Extension!.TrimStart('.').ToLowerInvariant()))
            .Where(item => !string.IsNullOrWhiteSpace(item.Ext))
            .ToList();

        var extensionSummary = string.Join(
            ", ",
            histogram
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(pair => $"{pair.Key}={pair.Value}"));

        var normalizedName = NormalizeName(node.Name, node.Path);

        if (IsSingleFileCandidate(node, sourceFiles))
        {
            var source = sourceFiles[0];
            var markers = new[] { "single-source-file" };
            var reason = $"single file candidate: {source.File.Name}";
            var fingerprint = BuildFingerprint(node, "SingleFileMiniProject", markers, histogram);
            return new DetectedProjectSuggestion(
                normalizedName,
                node.Path,
                "SingleFileMiniProject",
                CalculateScore(markers),
                reason,
                extensionSummary,
                markers,
                BuildTechHints(markers, histogram),
                fingerprint,
                DateTimeOffset.UtcNow);
        }

        if (IsNativeProjectCandidate(node, sourceFiles))
        {
            var markers = new[] { "main-source" };
            var reason = "native sources with entry/header layout";
            var fingerprint = BuildFingerprint(node, "ProjectRoot", markers, histogram);
            return new DetectedProjectSuggestion(
                normalizedName,
                node.Path,
                "ProjectRoot",
                CalculateScore(markers),
                reason,
                extensionSummary,
                markers,
                BuildTechHints(markers, histogram),
                fingerprint,
                DateTimeOffset.UtcNow);
        }

        if (IsStaticSiteCandidate(node, files))
        {
            var markers = new[] { "index.html" };
            var reason = "static site layout";
            var fingerprint = BuildFingerprint(node, "ProjectRoot", markers, histogram);
            return new DetectedProjectSuggestion(
                normalizedName,
                node.Path,
                "ProjectRoot",
                CalculateScore(markers),
                reason,
                extensionSummary,
                markers,
                BuildTechHints(markers, histogram),
                fingerprint,
                DateTimeOffset.UtcNow);
        }

        return null;
    }

    private static bool ShouldSkipHeuristicCandidate(DirectoryNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Name))
        {
            return false;
        }

        if (node.Name.StartsWith("_", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(node.Name, ".history", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSingleFileCandidate(
        DirectoryNode node,
        IReadOnlyList<SourceFileInfo> sourceFiles)
    {
        if (node.Directories.Count != 0 || node.Files.Count != 1 || sourceFiles.Count != 1)
        {
            return false;
        }

        return SingleFileProjectExtensions.Contains(sourceFiles[0].Ext);
    }

    private static bool IsNativeProjectCandidate(
        DirectoryNode node,
        IReadOnlyList<SourceFileInfo> sourceFiles)
    {
        if (sourceFiles.Count == 0)
        {
            return false;
        }

        var nativeSources = sourceFiles.Where(item => NativeSourceExtensions.Contains(item.Ext)).ToList();
        if (nativeSources.Count == 0)
        {
            return false;
        }

        var hasMain = nativeSources.Any(item => item.File.Name.StartsWith("main.", StringComparison.OrdinalIgnoreCase));
        var hasHeader = sourceFiles.Any(item => NativeHeaderExtensions.Contains(item.Ext));

        return hasMain || (hasHeader && nativeSources.Count >= 1);
    }

    private static bool IsStaticSiteCandidate(
        DirectoryNode node,
        IReadOnlyList<FileNode> files)
    {
        var htmlFiles = files
            .Count(file => file.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase));
        if (htmlFiles == 0)
        {
            return false;
        }

        var hasIndex = files.Any(file => string.Equals(file.Name, "index.html", StringComparison.OrdinalIgnoreCase));
        var hasCssDir = node.Directories.Any(dir => string.Equals(dir.Name, "css", StringComparison.OrdinalIgnoreCase));
        var hasJsDir = node.Directories.Any(dir => string.Equals(dir.Name, "js", StringComparison.OrdinalIgnoreCase));

        return hasIndex && (hasCssDir || hasJsDir || htmlFiles >= 2);
    }

    private static void Merge(Dictionary<string, int> target, Dictionary<string, int> source)
    {
        foreach (var (key, value) in source)
        {
            target[key] = target.GetValueOrDefault(key) + value;
        }
    }

    private static string BuildFingerprint(
        DirectoryNode node,
        string kind,
        IReadOnlyList<string> markers,
        Dictionary<string, int> histogram)
    {
        var builder = new StringBuilder();
        builder.Append("v1|");
        builder.Append(kind);
        builder.Append('|');

        foreach (var marker in markers.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("m:");
            builder.Append(marker.ToLowerInvariant());
            builder.Append('|');
        }

        foreach (var extension in histogram.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("e:");
            builder.Append(extension.ToLowerInvariant());
            builder.Append('=');
            builder.Append(histogram[extension]);
            builder.Append('|');
        }

        builder.Append("df:");
        builder.Append(node.Directories.Count);
        builder.Append("|ff:");
        builder.Append(node.Files.Count);
        builder.Append('|');

        foreach (var fileName in node.Files.Select(file => file.Name).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("f:");
            builder.Append(fileName.ToLowerInvariant());
            builder.Append('|');
        }

        foreach (var directoryName in node.Directories.Select(directory => directory.Name).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("d:");
            builder.Append(directoryName.ToLowerInvariant());
            builder.Append('|');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
