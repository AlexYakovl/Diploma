using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoSite.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewIsHidden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Reviews");

        }
    }
}
