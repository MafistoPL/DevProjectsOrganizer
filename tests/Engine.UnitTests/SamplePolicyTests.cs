using Engine.Scanning;
using FluentAssertions;
using Xunit;

namespace Engine.UnitTests;

public sealed class SamplePolicyTests
{
    [Theory]
    [InlineData("Program.cs", ".cs")]
    [InlineData("script.ts", ".ts")]
    [InlineData("readme.md", ".md")]
    [InlineData("CMakeLists.txt", "")]
    [InlineData("Makefile", "")]
    [InlineData("solution.sln", ".sln")]
    [InlineData("styles.scss", ".scss")]
    public void ShouldSample_returns_true_for_supported_files(string name, string extension)
    {
        SamplePolicy.ShouldSample(name, extension).Should().BeTrue();
    }

    [Theory]
    [InlineData("image.png", ".png")]
    [InlineData("archive.zip", ".zip")]
    [InlineData("", ".cs")]
    [InlineData("data", "")]
    public void ShouldSample_returns_false_for_unsupported_files(string name, string extension)
    {
        SamplePolicy.ShouldSample(name, extension).Should().BeFalse();
    }
}
