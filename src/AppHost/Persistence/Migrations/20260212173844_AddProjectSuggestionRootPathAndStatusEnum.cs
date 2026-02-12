using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppHost.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSuggestionRootPathAndStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RootPath",
                table: "project_suggestions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_project_suggestions_RootPath",
                table: "project_suggestions",
                column: "RootPath");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_suggestions_RootPath",
                table: "project_suggestions");

            migrationBuilder.DropColumn(
                name: "RootPath",
                table: "project_suggestions");
        }
    }
}
