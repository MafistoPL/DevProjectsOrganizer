using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppHost.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSuggestionFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                table: "project_suggestions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_project_suggestions_Path_Kind_Fingerprint",
                table: "project_suggestions",
                columns: new[] { "Path", "Kind", "Fingerprint" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_suggestions_Path_Kind_Fingerprint",
                table: "project_suggestions");

            migrationBuilder.DropColumn(
                name: "Fingerprint",
                table: "project_suggestions");
        }
    }
}
