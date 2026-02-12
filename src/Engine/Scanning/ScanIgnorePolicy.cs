namespace Engine.Scanning;

public static class ScanIgnorePolicy
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".vs",
        ".idea",
        "node_modules"
    };

    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdb",
        ".obj",
        ".tlog",
        ".exe",
        ".suo"
    };

    public static bool ShouldSkipDirectory(string? directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName))
        {
            return false;
        }

        return IgnoredDirectories.Contains(directoryName);
    }

    public static bool ShouldSkipFile(string? fileName, string? extension)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return IgnoredExtensions.Contains(extension);
    }
}
