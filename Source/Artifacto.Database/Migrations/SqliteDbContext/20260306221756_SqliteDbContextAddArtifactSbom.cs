using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artifacto.Database.Migrations.SqliteDbContext
{
    /// <inheritdoc />
    public partial class SqliteDbContextAddArtifactSbom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "SbomFileSizeBytes",
                table: "Artifacts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SbomSha256Hash",
                table: "Artifacts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SbomSpecVersion",
                table: "Artifacts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SbomFileSizeBytes",
                table: "Artifacts");

            migrationBuilder.DropColumn(
                name: "SbomSha256Hash",
                table: "Artifacts");

            migrationBuilder.DropColumn(
                name: "SbomSpecVersion",
                table: "Artifacts");
        }
    }
}
