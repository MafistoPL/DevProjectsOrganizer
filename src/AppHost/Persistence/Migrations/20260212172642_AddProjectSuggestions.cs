using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppHost.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_suggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScanSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    ExtensionsSummary = table.Column<string>(type: "TEXT", nullable: false),
                    MarkersJson = table.Column<string>(type: "TEXT", nullable: false),
                    TechHintsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_suggestions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_suggestions_Path",
                table: "project_suggestions",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_project_suggestions_ScanSessionId",
                table: "project_suggestions",
                column: "ScanSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_project_suggestions_Status",
                table: "project_suggestions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_suggestions");
        }
    }
}
