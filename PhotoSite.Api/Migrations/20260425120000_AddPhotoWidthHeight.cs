using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoSite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoWidthHeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "Photos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "Photos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Width",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "Photos");
        }
    }
}
