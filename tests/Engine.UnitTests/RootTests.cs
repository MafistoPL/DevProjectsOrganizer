using Engine.Models;
using FluentAssertions;
using Xunit;

namespace Engine.UnitTests;

public sealed class RootTests
{
    [Fact]
    public void Root_StoresValues()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var root = new Root(id, @"D:\code", "scanned", createdAt);

        root.Id.Should().Be(id);
        root.Path.Should().Be(@"D:\code");
        root.Status.Should().Be("scanned");
        root.CreatedAt.Should().Be(createdAt);
    }
}
