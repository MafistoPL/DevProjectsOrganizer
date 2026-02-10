using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppHost.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260210123000_AddScanSessions")]
public partial class AddScanSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "scan_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                RootId = table.Column<Guid>(type: "TEXT", nullable: true),
                RootPath = table.Column<string>(type: "TEXT", nullable: false),
                Mode = table.Column<string>(type: "TEXT", nullable: false),
                State = table.Column<string>(type: "TEXT", nullable: false),
                DiskKey = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                FinishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                FilesScanned = table.Column<long>(type: "INTEGER", nullable: false),
                TotalFiles = table.Column<long>(type: "INTEGER", nullable: true),
                CurrentPath = table.Column<string>(type: "TEXT", nullable: true),
                OutputPath = table.Column<string>(type: "TEXT", nullable: true),
                DepthLimit = table.Column<int>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_scan_sessions", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "scan_sessions");
    }
}
