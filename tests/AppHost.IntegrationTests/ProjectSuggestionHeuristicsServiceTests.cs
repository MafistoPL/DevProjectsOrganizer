using AppHost.Services;
using FluentAssertions;
using Xunit;

namespace AppHost.IntegrationTests;

public sealed class ProjectSuggestionHeuristicsServiceTests
{
    [Fact]
    public void Detect_creates_suggestion_when_marker_exists()
    {
        var snapshot = new ScanSnapshot
        {
            Roots =
            {
                new DirectoryNode
                {
                    Name = "dotnet-api",
                    Path = @"D:\code\dotnet-api",
                    Files =
                    {
                        new FileNode { Name = "Api.sln", Extension = ".sln" },
                        new FileNode { Name = "Api.csproj", Extension = ".csproj" },
                        new FileNode { Name = "Program.cs", Extension = ".cs" }
                    }
                }
            }
        };

        var sut = new ProjectSuggestionHeuristicsService();

        var results = sut.Detect(snapshot);

        results.Should().ContainSingle();
        var suggestion = results[0];
        suggestion.Name.Should().Be("dotnet-api");
        suggestion.Path.Should().Be(@"D:\code\dotnet-api");
        suggestion.Markers.Should().Contain(".sln");
        suggestion.Markers.Should().Contain(".csproj");
        suggestion.TechHints.Should().Contain("csharp");
        suggestion.ExtensionsSummary.Should().Contain("cs=");
    }

    [Fact]
    public void Detect_skips_directory_without_markers()
    {
        var snapshot = new ScanSnapshot
        {
            Roots =
            {
                new DirectoryNode
                {
                    Name = "notes",
                    Path = @"D:\notes",
                    Files =
                    {
                        new FileNode { Name = "readme.md", Extension = ".md" },
                        new FileNode { Name = "todo.txt", Extension = ".txt" }
                    }
                }
            }
        };

        var sut = new ProjectSuggestionHeuristicsService();

        var results = sut.Detect(snapshot);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Detect_includes_vcxproj_marker_as_project()
    {
        var snapshot = new ScanSnapshot
        {
            Roots =
            {
                new DirectoryNode
                {
                    Name = "CppTree",
                    Path = @"D:\old\CppTree\CppTree",
                    Files =
                    {
                        new FileNode { Name = "CppTree.sln", Extension = ".sln" },
                        new FileNode { Name = "CppTree.vcxproj", Extension = ".vcxproj" },
                        new FileNode { Name = "Tree.cpp", Extension = ".cpp" }
                    }
                }
            }
        };

        var sut = new ProjectSuggestionHeuristicsService();

        var results = sut.Detect(snapshot);

        results.Should().ContainSingle();
        results[0].Markers.Should().Contain(".vcxproj");
        results[0].Fingerprint.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Detect_creates_single_file_candidate_for_cpp_or_c()
    {
        var snapshot = new ScanSnapshot
        {
            Roots =
            {
                new DirectoryNode
                {
                    Name = "Chapter_01",
                    Path = @"D:\old\Beginning_C 1\Chapter_01",
                    Files =
                    {
                        new FileNode { Name = "simple.cpp", Extension = ".cpp" }
                    }
                },
                new DirectoryNode
                {
                    Name = "my_tests",
                    Path = @"D:\old\Beginning_C 1\my_tests",
                    Files =
                    {
                        new FileNode { Name = "test.c", Extension = ".c" }
                    }
                }
            }
        };

        var sut = new ProjectSuggestionHeuristicsService();

        var results = sut.Detect(snapshot);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(item => item.Kind == "SingleFileMiniProject");
        results.Select(item => item.Path).Should().Contain(@"D:\old\Beginning_C 1\Chapter_01");
        results.Select(item => item.Path).Should().Contain(@"D:\old\Beginning_C 1\my_tests");
    }

    [Fact]
    public void Detect_creates_native_project_for_main_cpp_and_headers()
    {
        var snapshot = new ScanSnapshot
        {
            Roots =
            {
                new DirectoryNode
                {
                    Name = "Linked_List",
                    Path = @"D:\old\Simple_Data_Structures\Linked_List",
                    Files =
                    {
                        new FileNode { Name = "main.cpp", Extension = ".cpp" },
                        new FileNode { Name = "LinkedList.h", Extension = ".h" }
                    }
                },
                new DirectoryNode
                {
                    Name = "Tree",
                    Path = @"D:\old\Simple_Data_Structures\Tree",
                    Files =
                    {
                        new FileNode { Name = "drzewo.c", Extension = ".c" },
                        new FileNode { Name = "drzewo.h", Extension = ".h" },
                        new FileNode { Name = "klub.c", Extension = ".c" }
                    }
                }
            }
        };

        var sut = new ProjectSuggestionHeuristicsService();

        var results = sut.Detect(snapshot);

        results.Select(item => item.Path).Should().Contain(@"D:\old\Simple_Data_Structures\Linked_List");
        results.Select(item => item.Path).Should().Contain(@"D:\old\Simple_Data_Structures\Tree");
    }

    [Fact]
    public void Detect_creates_static_site_for_index_with_css_js_and_skips_underscored_copy()
    {
        var snapshot = new ScanSnapshot
        {
            Roots =
            {
                new DirectoryNode
                {
                    Name = "aleksandra-wiejaczka-strona",
                    Path = @"D:\old\aleksandra-wiejaczka-strona",
                    Directories =
                    {
                        new DirectoryNode { Name = "css", Path = @"D:\old\aleksandra-wiejaczka-strona\css" },
                        new DirectoryNode { Name = "js", Path = @"D:\old\aleksandra-wiejaczka-strona\js" },
                        new DirectoryNode
                        {
                            Name = "_strona-oli",
                            Path = @"D:\old\aleksandra-wiejaczka-strona\_strona-oli",
                            Files =
                            {
                                new FileNode { Name = "index.html", Extension = ".html" }
                            }
                        }
                    },
                    Files =
                    {
                        new FileNode { Name = "index.html", Extension = ".html" },
                        new FileNode { Name = "kontakt.html", Extension = ".html" }
                    }
                }
            }
        };

        var sut = new ProjectSuggestionHeuristicsService();

        var results = sut.Detect(snapshot);

        results.Select(item => item.Path).Should().Contain(@"D:\old\aleksandra-wiejaczka-strona");
        results.Select(item => item.Path).Should().NotContain(@"D:\old\aleksandra-wiejaczka-strona\_strona-oli");
    }

    [Fact]
    public void Detect_generates_stable_fingerprint_for_same_snapshot_shape()
    {
        var snapshot = new ScanSnapshot
        {
            Roots =
            {
                new DirectoryNode
                {
                    Name = "2Dsource",
                    Path = @"D:\old\2Dsource",
                    Files =
                    {
                        new FileNode { Name = "2Dsource.vcxproj", Extension = ".vcxproj" },
                        new FileNode { Name = "main.cpp", Extension = ".cpp" }
                    }
                }
            }
        };

        var sut = new ProjectSuggestionHeuristicsService();

        var first = sut.Detect(snapshot).Single();
        var second = sut.Detect(snapshot).Single();

        first.Fingerprint.Should().Be(second.Fingerprint);
    }
}
