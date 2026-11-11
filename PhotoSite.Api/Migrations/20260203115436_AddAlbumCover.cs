using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoSite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbumCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "title",
                table: "Albums",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Albums",
                newName: "Id");

            migrationBuilder.AddColumn<string>(
                name: "CoverPath",
                table: "Albums",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverPath",
                table: "Albums");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Albums",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Albums",
                newName: "id");
        }
    }
}
