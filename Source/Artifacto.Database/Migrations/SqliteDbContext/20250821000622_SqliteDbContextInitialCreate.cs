using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artifacto.Database.Migrations.SqliteDbContext;

/// <inheritdoc />
public partial class SqliteDbContextInitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new
            {
                ProjectId = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: true),
                Description = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Projects", x => x.ProjectId));

        migrationBuilder.CreateTable(
            name: "Artifacts",
            columns: table => new
            {
                ArtifactId = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Version = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                FileName = table.Column<string>(type: "TEXT", nullable: false),
                FileSizeBytes = table.Column<ulong>(type: "INTEGER", nullable: false),
                Sha256Hash = table.Column<string>(type: "TEXT", nullable: false),
                Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                Retained = table.Column<bool>(type: "INTEGER", nullable: false),
                Locked = table.Column<bool>(type: "INTEGER", nullable: false),
                ProjectId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Artifacts", x => x.ArtifactId);
                table.ForeignKey(
                    name: "FK_Artifacts_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "ProjectId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Artifacts_ProjectId_Version",
            table: "Artifacts",
            columns: ["ProjectId", "Version"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Projects_Key",
            table: "Projects",
            column: "Key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Artifacts");

        migrationBuilder.DropTable(
            name: "Projects");
    }
}
