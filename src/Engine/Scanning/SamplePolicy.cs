namespace Engine.Scanning;

public static class SamplePolicy
{
    private static readonly HashSet<string> SampleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".js", ".json", ".md",
        ".cpp", ".h", ".hpp", ".c",
        ".py", ".ps1", ".go", ".rs",
        ".java", ".kt", ".xml", ".yml", ".yaml",
        ".html", ".css", ".scss",
        ".sln", ".slnx", ".csproj", ".fsproj", ".vbproj", ".vcxproj"
    };

    private static readonly HashSet<string> SampleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Makefile",
        "CMakeLists.txt"
    };

    public static bool ShouldSample(string fileName, string extension)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (SampleNames.Contains(fileName))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return SampleExtensions.Contains(extension);
    }
}
