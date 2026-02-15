using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppHost.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTagSystemFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "tags",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE tags
                SET IsSystem = 1
                WHERE NormalizedName IN (
                    'csharp',
                    'dotnet',
                    'cpp',
                    'c',
                    'native',
                    'vs-solution',
                    'vs-project',
                    'node',
                    'react',
                    'angular',
                    'html',
                    'json',
                    'git',
                    'cmake',
                    'makefile',
                    'java',
                    'gradle',
                    'maven',
                    'python',
                    'rust',
                    'go',
                    'powershell',
                    'low-level',
                    'console',
                    'winapi',
                    'gui'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "tags");
        }
    }
}
