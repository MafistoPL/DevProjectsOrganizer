using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppHost.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTagSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_tags",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_tags", x => new { x.ProjectId, x.TagId });
                    table.ForeignKey(
                        name: "FK_project_tags_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_tags_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tag_suggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SuggestedTagName = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tag_suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tag_suggestions_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tag_suggestions_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_tags_TagId",
                table: "project_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_tag_suggestions_CreatedAt",
                table: "tag_suggestions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tag_suggestions_ProjectId",
                table: "tag_suggestions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_tag_suggestions_Status",
                table: "tag_suggestions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tag_suggestions_TagId",
                table: "tag_suggestions",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_tags");

            migrationBuilder.DropTable(
                name: "tag_suggestions");
        }
    }
}
