using Engine.Scanning;
using FluentAssertions;
using Xunit;

namespace Engine.UnitTests;

public sealed class ScanIgnorePolicyTests
{
    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData(".vs")]
    [InlineData(".idea")]
    [InlineData("node_modules")]
    [InlineData("BIN")]
    public void ShouldSkipDirectory_returns_true_for_configured_directories(string directoryName)
    {
        ScanIgnorePolicy.ShouldSkipDirectory(directoryName).Should().BeTrue();
    }

    [Theory]
    [InlineData("src")]
    [InlineData("docs")]
    [InlineData("assets")]
    [InlineData("")]
    public void ShouldSkipDirectory_returns_false_for_other_directories(string directoryName)
    {
        ScanIgnorePolicy.ShouldSkipDirectory(directoryName).Should().BeFalse();
    }

    [Theory]
    [InlineData("app.pdb", ".pdb")]
    [InlineData("module.obj", ".obj")]
    [InlineData("build.tlog", ".tlog")]
    [InlineData("app.exe", ".exe")]
    [InlineData("solution.suo", ".suo")]
    [InlineData("APP.PDB", ".PDB")]
    public void ShouldSkipFile_returns_true_for_configured_extensions(string fileName, string extension)
    {
        ScanIgnorePolicy.ShouldSkipFile(fileName, extension).Should().BeTrue();
    }

    [Theory]
    [InlineData("Program.cs", ".cs")]
    [InlineData("README.md", ".md")]
    [InlineData("package.json", ".json")]
    [InlineData("file_without_extension", "")]
    [InlineData("", ".exe")]
    public void ShouldSkipFile_returns_false_for_other_files(string fileName, string extension)
    {
        ScanIgnorePolicy.ShouldSkipFile(fileName, extension).Should().BeFalse();
    }
}
