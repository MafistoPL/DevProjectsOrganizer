using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;
using AppHost.Persistence;

namespace AppHost.Services;

public sealed class TagSuggestionHeuristicsService
{
    private static readonly Regex HelloWorldPattern = new(
        @"\bhello[\W_]*world\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LoremIpsumPattern = new(
        @"\blorem[\s_\-]*ipsum\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PointerDeclarationPattern = new(
        @"\b(?:const\s+)?(?:unsigned\s+|signed\s+)?[a-z_][\w:<>]*\s*\*+\s*[a-z_]\w*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PointerArrowPattern = new(
        @"->",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AsmKeywordPattern = new(
        @"\b(?:__asm__?|asm)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex WinMainPattern = new(
        @"\b(?:int\s+)?(?:winapi\s+)?winmain\s*\(",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> SourceSignalExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".c",
        ".h",
        ".hpp",
        ".cpp",
        ".cc",
        ".cxx",
        ".asm",
        ".s",
        ".cs",
        ".java",
        ".js",
        ".jsx",
        ".ts",
        ".tsx",
        ".py",
        ".go",
        ".rs",
        ".html",
        ".txt",
        ".md"
    };

    private static readonly HashSet<string> ProjectSizeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".c",
        ".h",
        ".hpp",
        ".cpp",
        ".cc",
        ".cxx",
        ".asm",
        ".s",
        ".cs",
        ".java",
        ".js",
        ".jsx",
        ".ts",
        ".tsx",
        ".py",
        ".go",
        ".rs",
        ".ps1"
    };

    private const int MaxSourceFilesToInspect = 120;
    private const int MaxSourceLinesPerFile = 120;
    private const int MaxSourceLineLength = 512;
    private const long MaxSourceFileBytes = 512 * 1024;
    private const int MaxProjectSourceFilesToInspect = 2_500;
    private const long MaxProjectSourceFileBytes = 4 * 1024 * 1024;
    private const int MaxProjectLinesToClassify = 100_001;

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
                    break;
                case "native":
                    AddSignal("native", 0.8, $"hint:{normalized}");
                    break;
                case "asm":
                case "assembly":
                    AddSignal("low-level", 0.84, $"hint:{normalized}");
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
                case "asm":
                case "s":
                    AddSignal("low-level", Math.Min(0.92, 0.7 + count * 0.03), $"ext:{ext}={count}");
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
                case "java":
                    var mapped = ext switch
                    {
                        "go" => "go",
                        "rs" => "rust",
                        "py" => "python",
                        "java" => "java",
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

        if (LooksLikeBeginnerHelloWorldProject(project))
        {
            AddSignal("hello-world", 0.74, "path:beginner-chapter");
        }

        var sourceSignals = DetectSourceSignals(project.Path);
        if (sourceSignals.HelloWorldEvidence is not null)
        {
            AddSignal("hello-world", 0.9, sourceSignals.HelloWorldEvidence);
        }

        if (sourceSignals.LoremIpsumEvidence is not null)
        {
            AddSignal("lorem-ipsum", 0.9, sourceSignals.LoremIpsumEvidence);
        }

        if (sourceSignals.AsmEvidence is not null)
        {
            AddSignal("low-level", 0.88, sourceSignals.AsmEvidence);
        }

        if (sourceSignals.WinMainEvidence is not null)
        {
            AddSignal("winapi", 0.9, sourceSignals.WinMainEvidence);
        }

        if (sourceSignals.PointerEvidence is not null
            && LooksLikeCorCppProject(markers, hints, extensionHistogram, project.Path, project.Reason))
        {
            AddSignal("pointers", 0.85, sourceSignals.PointerEvidence);
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

        var projectLineCount = CountProjectSourceLines(project.Path);
        if (projectLineCount >= 0 && TryResolveProjectSizeTag(projectLineCount, out var projectSizeTag))
        {
            AddSignal(projectSizeTag, 0.88, $"lines:{projectLineCount}");
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

    private static bool LooksLikeBeginnerHelloWorldProject(ProjectEntity project)
    {
        var path = project.Path.ToLowerInvariant();
        var name = project.Name.ToLowerInvariant();
        var reason = project.Reason.ToLowerInvariant();

        if (path.Contains("hello-world", StringComparison.OrdinalIgnoreCase)
            || path.Contains("hello_world", StringComparison.OrdinalIgnoreCase)
            || path.Contains("helloworld", StringComparison.OrdinalIgnoreCase)
            || name.Contains("hello-world", StringComparison.OrdinalIgnoreCase)
            || name.Contains("hello_world", StringComparison.OrdinalIgnoreCase)
            || name.Contains("helloworld", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var hasChapter01 =
            path.Contains("chapter_01", StringComparison.OrdinalIgnoreCase)
            || path.Contains("chapter-01", StringComparison.OrdinalIgnoreCase)
            || path.Contains("chapter01", StringComparison.OrdinalIgnoreCase)
            || name.Contains("chapter_01", StringComparison.OrdinalIgnoreCase)
            || name.Contains("chapter-01", StringComparison.OrdinalIgnoreCase)
            || name.Contains("chapter01", StringComparison.OrdinalIgnoreCase);

        var hasBeginnerSignal =
            path.Contains("beginning_c", StringComparison.OrdinalIgnoreCase)
            || path.Contains("beginning c", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("single file candidate", StringComparison.OrdinalIgnoreCase);

        return hasChapter01 && hasBeginnerSignal;
    }

    private static bool LooksLikeCorCppProject(
        IReadOnlyCollection<string> markers,
        IReadOnlyCollection<string> hints,
        IReadOnlyDictionary<string, int> extensionHistogram,
        string projectPath,
        string projectReason)
    {
        if (markers.Any(marker =>
                marker.Equals(".vcxproj", StringComparison.OrdinalIgnoreCase)
                || marker.Equals(".vcproj", StringComparison.OrdinalIgnoreCase)
                || marker.Equals("cmakelists.txt", StringComparison.OrdinalIgnoreCase)
                || marker.Equals("makefile", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (hints.Any(hint =>
                hint.Equals("c", StringComparison.OrdinalIgnoreCase)
                || hint.Equals("cpp", StringComparison.OrdinalIgnoreCase)
                || hint.Equals("native", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (extensionHistogram.ContainsKey("c")
            || extensionHistogram.ContainsKey("h")
            || extensionHistogram.ContainsKey("hpp")
            || extensionHistogram.ContainsKey("cpp")
            || extensionHistogram.ContainsKey("cc")
            || extensionHistogram.ContainsKey("cxx"))
        {
            return true;
        }

        return projectPath.Contains("cpp", StringComparison.OrdinalIgnoreCase)
            || projectReason.Contains("native", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountProjectSourceLines(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
        {
            return -1;
        }

        var inspected = 0;
        var totalLines = 0;
        foreach (var filePath in EnumerateProjectSizeFiles(projectPath))
        {
            if (inspected >= MaxProjectSourceFilesToInspect || totalLines >= MaxProjectLinesToClassify)
            {
                break;
            }

            inspected++;
            var remaining = MaxProjectLinesToClassify - totalLines;
            totalLines += CountLines(filePath, remaining);
        }

        return Math.Min(totalLines, MaxProjectLinesToClassify);
    }

    private static int CountLines(string filePath, int maxLinesToCount)
    {
        if (maxLinesToCount <= 0)
        {
            return 0;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            var count = 0;
            while (count < maxLinesToCount && reader.ReadLine() is not null)
            {
                count++;
            }

            return count;
        }
        catch
        {
            return 0;
        }
    }

    private static bool TryResolveProjectSizeTag(int lineCount, out string tagName)
    {
        tagName = string.Empty;
        if (lineCount < 100)
        {
            tagName = "lines-lt-100";
            return true;
        }

        if (lineCount < 200)
        {
            tagName = "lines-100-200";
            return true;
        }

        if (lineCount < 500)
        {
            tagName = "lines-200-500";
            return true;
        }

        if (lineCount < 1_000)
        {
            tagName = "lines-500-1k";
            return true;
        }

        if (lineCount < 2_000)
        {
            tagName = "lines-1k-2k";
            return true;
        }

        if (lineCount < 5_000)
        {
            tagName = "lines-2k-5k";
            return true;
        }

        if (lineCount >= 10_000 && lineCount < 20_000)
        {
            tagName = "lines-10k-20k";
            return true;
        }

        if (lineCount >= 20_000 && lineCount < 50_000)
        {
            tagName = "lines-20k-50k";
            return true;
        }

        if (lineCount >= 50_000 && lineCount < 100_000)
        {
            tagName = "lines-50k-100k";
            return true;
        }

        if (lineCount >= 100_000)
        {
            tagName = "lines-gt-100k";
            return true;
        }

        return false;
    }

    private static SourceSignalDetectionResult DetectSourceSignals(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
        {
            return SourceSignalDetectionResult.None;
        }

        var inspected = 0;
        string? helloWorldEvidence = null;
        string? loremIpsumEvidence = null;
        string? pointerEvidence = null;
        string? asmEvidence = null;
        string? winMainEvidence = null;

        foreach (var file in EnumerateCandidateSourceFiles(projectPath))
        {
            if (inspected >= MaxSourceFilesToInspect)
            {
                break;
            }

            inspected++;
            var (hasHelloWorld, hasLoremIpsum, hasPointer, hasAsm, hasWinMain) = ScanFileForSignals(file);
            if (hasHelloWorld && helloWorldEvidence is null)
            {
                helloWorldEvidence = $"code:hello-world:{Path.GetFileName(file)}";
            }

            if (hasLoremIpsum && loremIpsumEvidence is null)
            {
                loremIpsumEvidence = $"code:lorem-ipsum:{Path.GetFileName(file)}";
            }

            if (hasPointer && pointerEvidence is null)
            {
                pointerEvidence = $"code:pointers:{Path.GetFileName(file)}";
            }

            if (hasAsm && asmEvidence is null)
            {
                asmEvidence = $"code:asm:{Path.GetFileName(file)}";
            }

            if (hasWinMain && winMainEvidence is null)
            {
                winMainEvidence = $"code:winmain:{Path.GetFileName(file)}";
            }

            if (helloWorldEvidence is not null
                && loremIpsumEvidence is not null
                && pointerEvidence is not null
                && asmEvidence is not null
                && winMainEvidence is not null)
            {
                break;
            }
        }

        if (helloWorldEvidence is null
            && loremIpsumEvidence is null
            && pointerEvidence is null
            && asmEvidence is null
            && winMainEvidence is null)
        {
            return SourceSignalDetectionResult.None;
        }

        return new SourceSignalDetectionResult(
            helloWorldEvidence,
            loremIpsumEvidence,
            pointerEvidence,
            asmEvidence,
            winMainEvidence);
    }

    private static IEnumerable<string> EnumerateProjectSizeFiles(string rootPath)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> subdirectories;
            try
            {
                subdirectories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                subdirectories = Array.Empty<string>();
            }

            foreach (var subdirectory in subdirectories)
            {
                pending.Push(subdirectory);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                files = Array.Empty<string>();
            }

            foreach (var file in files)
            {
                if (!IsProjectSizeSourceFile(file))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateCandidateSourceFiles(string rootPath)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> subdirectories;
            try
            {
                subdirectories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                subdirectories = Array.Empty<string>();
            }

            foreach (var subdirectory in subdirectories)
            {
                pending.Push(subdirectory);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                files = Array.Empty<string>();
            }

            foreach (var file in files)
            {
                if (!IsCandidateSourceFile(file))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static bool IsCandidateSourceFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        if (!SourceSignalExtensions.Contains(extension))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(filePath);
            return info.Exists && info.Length <= MaxSourceFileBytes;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProjectSizeSourceFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension) || !ProjectSizeExtensions.Contains(extension))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(filePath);
            return info.Exists && info.Length <= MaxProjectSourceFileBytes;
        }
        catch
        {
            return false;
        }
    }

    private static (bool HasHelloWorld, bool HasLoremIpsum, bool HasPointer, bool HasAsm, bool HasWinMain) ScanFileForSignals(
        string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);

            var hasHelloWorld = false;
            var hasLoremIpsum = false;
            var hasPointer = false;
            var hasAsm = false;
            var hasWinMain = false;
            for (var lineNumber = 0; lineNumber < MaxSourceLinesPerFile; lineNumber++)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (line.Length > MaxSourceLineLength)
                {
                    line = line[..MaxSourceLineLength];
                }

                if (!hasHelloWorld && HelloWorldPattern.IsMatch(line))
                {
                    hasHelloWorld = true;
                }

                if (!hasLoremIpsum && LoremIpsumPattern.IsMatch(line))
                {
                    hasLoremIpsum = true;
                }

                if (!hasPointer && (PointerDeclarationPattern.IsMatch(line) || PointerArrowPattern.IsMatch(line)))
                {
                    hasPointer = true;
                }

                if (!hasAsm && AsmKeywordPattern.IsMatch(line))
                {
                    hasAsm = true;
                }

                if (!hasWinMain && WinMainPattern.IsMatch(line))
                {
                    hasWinMain = true;
                }

                if (hasHelloWorld && hasLoremIpsum && hasPointer && hasAsm && hasWinMain)
                {
                    break;
                }
            }

            var extension = Path.GetExtension(filePath);
            if (!hasAsm && extension.Equals(".asm", StringComparison.OrdinalIgnoreCase))
            {
                hasAsm = true;
            }

            if (!hasAsm && extension.Equals(".s", StringComparison.OrdinalIgnoreCase))
            {
                hasAsm = true;
            }

            return (hasHelloWorld, hasLoremIpsum, hasPointer, hasAsm, hasWinMain);
        }
        catch
        {
            return (false, false, false, false, false);
        }
    }

    private sealed class TagSignal
    {
        public double Confidence { get; set; }
        public HashSet<string> Evidences { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record SourceSignalDetectionResult(
        string? HelloWorldEvidence,
        string? LoremIpsumEvidence,
        string? PointerEvidence,
        string? AsmEvidence,
        string? WinMainEvidence)
    {
        public static readonly SourceSignalDetectionResult None = new(null, null, null, null, null);
    }
}
