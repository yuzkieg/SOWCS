using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT15_SOWCS.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveRestoreSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "serialized_data",
                table: "ArchiveItem",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_id",
                table: "ArchiveItem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                table: "ArchiveItem",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "serialized_data",
                table: "ArchiveItem");

            migrationBuilder.DropColumn(
                name: "source_id",
                table: "ArchiveItem");

            migrationBuilder.DropColumn(
                name: "source_type",
                table: "ArchiveItem");
        }
    }
}
