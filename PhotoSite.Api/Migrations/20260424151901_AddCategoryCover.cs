using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoSite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverPath",
                table: "AlbumCategories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverPath",
                table: "AlbumCategories");
        }
    }
}
