using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaveRadar.Api.Migrations
{
    /// <inheritdoc />
    public partial class ModernizeModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FavoriteSongs",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceId",
                table: "Events",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FavoriteSongs",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "Events");
        }
    }
}
