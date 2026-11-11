using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PhotoSite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbumCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlbumCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumCategories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AlbumCategories",
                columns: new[] { "Id", "Title", "Order", "CreatedAt" },
                values: new object[] { 1, "Default", 0, DateTime.UtcNow });

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Albums",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Albums_CategoryId",
                table: "Albums",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_AlbumCategories_CategoryId",
                table: "Albums",
                column: "CategoryId",
                principalTable: "AlbumCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Albums_AlbumCategories_CategoryId",
                table: "Albums");

            migrationBuilder.DropTable(
                name: "AlbumCategories");

            migrationBuilder.DropIndex(
                name: "IX_Albums_CategoryId",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Albums");
        }
    }
}
